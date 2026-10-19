using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using PurrNet;
using PurrNet.Packing;

namespace VALYR.Multiplayer
{
    /// <summary>
    /// Network-replicated lighting data for vehicle illumination, turn signals, and dynamic cues.
    /// Bit-packed with zero memory allocations using PurrNet IPackedAuto.
    /// </summary>
    [Serializable]
    public struct VehicleLightsNetworkData : IPackedAuto, IPacked
    {
        public byte HeadlightMode;   // 0=Off, 1=LowBeam, 2=HighBeam
        public bool IsLeftTurn;
        public bool IsRightTurn;
        public bool IsHazard;
        public bool IsBraking;
        public bool IsReversing;
        public bool IsHandbraking;

        public void Write(BitPacker packer)
        {
            packer.WriteBits(HeadlightMode, 8);
            packer.WriteBit(IsLeftTurn);
            packer.WriteBit(IsRightTurn);
            packer.WriteBit(IsHazard);
            packer.WriteBit(IsBraking);
            packer.WriteBit(IsReversing);
            packer.WriteBit(IsHandbraking);
        }

        public void Read(BitPacker packer)
        {
            HeadlightMode = (byte)packer.ReadBits(8);
            IsLeftTurn = packer.ReadBit();
            IsRightTurn = packer.ReadBit();
            IsHazard = packer.ReadBit();
            IsBraking = packer.ReadBit();
            IsReversing = packer.ReadBit();
            IsHandbraking = packer.ReadBit();
        }
    }

    /// <summary>
    /// Synchronizes vehicle exterior and interior lights over PurrNet.
    /// Handles headlight modes (Off/Low/High), turn signals & hazards with blinking and audio ticks,
    /// dynamic automatic brake/reverse lights, and input isolation.
    /// </summary>
    [RequireComponent(typeof(NetworkVehicle))]
    public class NetworkVehicleLights : NetworkBehaviour
    {
        [Header("PurrNet State")]
        [Tooltip("Owner-authoritative lighting state.")]
        [SerializeField]
        private SyncVar<VehicleLightsNetworkData> _lightsState = new(default, 0.05f, ownerAuth: true);

        private NetworkVehicle _netVehicle;
        private Component _lightController;

        // Cached light GameObjects and Lights from VehicleLightController
        private GameObject[] _lowBeamHeadlights;
        private Light[] _lowBeamSpotlights;
        private GameObject[] _highBeamHeadlights;
        private Light[] _highBeamSpotlights;
        private GameObject[] _rearRunningLights;
        private GameObject[] _brakeLights;
        private GameObject[] _reverseLights;
        private GameObject[] _handbrakeLights;
        private GameObject[] _leftTurnLights;
        private GameObject[] _rightTurnLights;
        private GameObject[] _interiorLights;
        private float _blinkPeriod = 0.40f;
        private AudioSource _audioSource;
        private AudioClip _tickSound;

        // Blinking state for remote proxies
        private float _blinkTimer;
        private bool _blinkVisible;

        // Reflection cache for VALYR.Runtime.Visual.VehicleLightController (Assembly-CSharp)
        private static readonly Type LightControllerType =
            Type.GetType("VALYR.Runtime.Visual.VehicleLightController, Assembly-CSharp");

        private static readonly FieldInfo LowBeamHeadlightsField = LightControllerType?.GetField("LowBeamHeadlights");
        private static readonly FieldInfo LowBeamSpotlightsField = LightControllerType?.GetField("LowBeamSpotlights");
        private static readonly FieldInfo HighBeamHeadlightsField = LightControllerType?.GetField("HighBeamHeadlights");
        private static readonly FieldInfo HighBeamSpotlightsField = LightControllerType?.GetField("HighBeamSpotlights");
        private static readonly FieldInfo RearRunningLightsField = LightControllerType?.GetField("RearRunningLights");
        private static readonly FieldInfo BrakeLightsField = LightControllerType?.GetField("BrakeLights");
        private static readonly FieldInfo ReverseLightsField = LightControllerType?.GetField("ReverseLights");
        private static readonly FieldInfo HandbrakeLightsField = LightControllerType?.GetField("HandbrakeLights");
        private static readonly FieldInfo LeftTurnLightsField = LightControllerType?.GetField("LeftTurnLights");
        private static readonly FieldInfo RightTurnLightsField = LightControllerType?.GetField("RightTurnLights");
        private static readonly FieldInfo BlinkPeriodField = LightControllerType?.GetField("BlinkPeriod");
        private static readonly FieldInfo InteriorLightsField = LightControllerType?.GetField("InteriorLights");
        private static readonly FieldInfo AudioSourceField =
            LightControllerType?.GetField("_audioSource", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo TurnSignalTickSoundField =
            LightControllerType?.GetField("_turnSignalTickSound", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly PropertyInfo CurrentHeadlightModeProp = LightControllerType?.GetProperty("CurrentHeadlightMode");
        private static readonly PropertyInfo IsLeftTurnActiveProp = LightControllerType?.GetProperty("IsLeftTurnActive");
        private static readonly PropertyInfo IsRightTurnActiveProp = LightControllerType?.GetProperty("IsRightTurnActive");
        private static readonly PropertyInfo IsHazardActiveProp = LightControllerType?.GetProperty("IsHazardActive");

        private static readonly MethodInfo CycleHeadlightsMethod = LightControllerType?.GetMethod("CycleHeadlights");
        private static readonly MethodInfo ToggleHighBeamMethod = LightControllerType?.GetMethod("ToggleHighBeam");
        private static readonly MethodInfo ToggleLeftTurnMethod = LightControllerType?.GetMethod("ToggleLeftTurn");
        private static readonly MethodInfo ToggleRightTurnMethod = LightControllerType?.GetMethod("ToggleRightTurn");
        private static readonly MethodInfo ToggleHazardMethod = LightControllerType?.GetMethod("ToggleHazard");
        private static readonly MethodInfo UnsubscribeInputActionsMethod =
            LightControllerType?.GetMethod("UnsubscribeInputActions", BindingFlags.NonPublic | BindingFlags.Instance);

        private void Awake()
        {
            _netVehicle = GetComponent<NetworkVehicle>();
            if (LightControllerType != null)
            {
                _lightController = GetComponent(LightControllerType);
            }

            CacheVisualReferences();
        }

        private void CacheVisualReferences()
        {
            if (_lightController == null) return;

            _lowBeamHeadlights = LowBeamHeadlightsField?.GetValue(_lightController) as GameObject[];
            _lowBeamSpotlights = LowBeamSpotlightsField?.GetValue(_lightController) as Light[];
            _highBeamHeadlights = HighBeamHeadlightsField?.GetValue(_lightController) as GameObject[];
            _highBeamSpotlights = HighBeamSpotlightsField?.GetValue(_lightController) as Light[];
            _rearRunningLights = RearRunningLightsField?.GetValue(_lightController) as GameObject[];
            _brakeLights = BrakeLightsField?.GetValue(_lightController) as GameObject[];
            _reverseLights = ReverseLightsField?.GetValue(_lightController) as GameObject[];
            _handbrakeLights = HandbrakeLightsField?.GetValue(_lightController) as GameObject[];
            _leftTurnLights = LeftTurnLightsField?.GetValue(_lightController) as GameObject[];
            _rightTurnLights = RightTurnLightsField?.GetValue(_lightController) as GameObject[];
            _interiorLights = InteriorLightsField?.GetValue(_lightController) as GameObject[];

            if (BlinkPeriodField != null)
            {
                object periodObj = BlinkPeriodField.GetValue(_lightController);
                if (periodObj != null) _blinkPeriod = (float)periodObj;
            }

            _audioSource = AudioSourceField?.GetValue(_lightController) as AudioSource;
            _tickSound = TurnSignalTickSoundField?.GetValue(_lightController) as AudioClip;
        }

        private void Start()
        {
            IsolateLightControllerInput();
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();
            IsolateLightControllerInput();
        }

        /// <summary>
        /// Prevents VehicleLightController on proxy trucks from processing keys or fighting network states.
        /// On remote proxy, disables the local controller entirely and lets NetworkVehicleLights render visuals.
        /// </summary>
        public void IsolateLightControllerInput()
        {
            if (_lightController == null) return;

            bool isLocal = _netVehicle != null && _netVehicle.IsLocallyControlled;
            var mb = _lightController as MonoBehaviour;

            if (isLocal)
            {
                if (mb != null) mb.enabled = true;

                // Neutralize fallback keys in VehicleLightController so this component is single authoritative handler
                UnsubscribeInputActionsMethod?.Invoke(_lightController, null);
                var dummyAction = new InputAction("NetworkLightDummy");
                SetPrivateField(_lightController, "_headlightsActionRef", dummyAction);
                SetPrivateField(_lightController, "_highBeamActionRef", dummyAction);
                SetPrivateField(_lightController, "_leftTurnActionRef", dummyAction);
                SetPrivateField(_lightController, "_rightTurnActionRef", dummyAction);
                SetPrivateField(_lightController, "_hazardActionRef", dummyAction);
            }
            else
            {
                // Disable local controller on proxies so its Update() loop doesn't overwrite brake lights or play sounds
                if (mb != null) mb.enabled = false;
            }
        }

        private void Update()
        {
            if (_lightController == null) return;

            bool isLocal = _netVehicle != null && _netVehicle.IsLocallyControlled;

            if (isLocal)
            {
                // 1. Local driver keyboard input (L=Headlights, K=HighBeam, Q=LeftTurn, E=RightTurn, H=Hazard)
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.lKey.wasPressedThisFrame)
                        CycleHeadlightsMethod?.Invoke(_lightController, null);

                    if (kb.kKey.wasPressedThisFrame)
                        ToggleHighBeamMethod?.Invoke(_lightController, null);

                    if (kb.qKey.wasPressedThisFrame)
                        ToggleLeftTurnMethod?.Invoke(_lightController, null);

                    if (kb.eKey.wasPressedThisFrame)
                        ToggleRightTurnMethod?.Invoke(_lightController, null);

                    if (kb.hKey.wasPressedThisFrame)
                        ToggleHazardMethod?.Invoke(_lightController, null);
                }

                // 2. Read driving & light status from local controller and vehicle state
                byte mode = 0;
                bool isLeft = false;
                bool isRight = false;
                bool isHazard = false;

                if (CurrentHeadlightModeProp != null)
                    mode = Convert.ToByte(CurrentHeadlightModeProp.GetValue(_lightController));
                if (IsLeftTurnActiveProp != null)
                    isLeft = (bool)IsLeftTurnActiveProp.GetValue(_lightController);
                if (IsRightTurnActiveProp != null)
                    isRight = (bool)IsRightTurnActiveProp.GetValue(_lightController);
                if (IsHazardActiveProp != null)
                    isHazard = (bool)IsHazardActiveProp.GetValue(_lightController);

                VehicleNetworkState netState = _netVehicle.SyncedState;

                var data = new VehicleLightsNetworkData
                {
                    HeadlightMode = mode,
                    IsLeftTurn = isLeft,
                    IsRightTurn = isRight,
                    IsHazard = isHazard,
                    IsBraking = netState.IsBraking,
                    IsReversing = netState.IsReversing,
                    IsHandbraking = netState.IsHandbraking
                };

                if (!AreStatesEqual(in data, _lightsState.value))
                {
                    _lightsState.value = data;
                }
            }
            else
            {
                // Remote proxy: animate lights according to received network data
                RenderProxyLights(Time.deltaTime);
            }
        }

        private void RenderProxyLights(float dt)
        {
            VehicleLightsNetworkData data = _lightsState.value;

            // 1. Headlights & Running Lights
            bool hasLow = data.HeadlightMode == 1 || data.HeadlightMode == 2;
            bool hasHigh = data.HeadlightMode == 2;

            SetGroupActive(_lowBeamHeadlights, hasLow);
            SetSpotlightsActive(_lowBeamSpotlights, data.HeadlightMode == 1);

            SetGroupActive(_highBeamHeadlights, hasHigh);
            SetSpotlightsActive(_highBeamSpotlights, hasHigh);

            SetGroupActive(_rearRunningLights, hasLow);

            // 2. Turn Signals & Hazard Blinking
            if (data.IsLeftTurn || data.IsRightTurn || data.IsHazard)
            {
                _blinkTimer += dt;
                if (_blinkTimer >= _blinkPeriod)
                {
                    _blinkTimer -= _blinkPeriod;
                    _blinkVisible = !_blinkVisible;

                    if (_blinkVisible && _audioSource != null && _tickSound != null)
                    {
                        _audioSource.PlayOneShot(_tickSound);
                    }
                }
            }
            else
            {
                _blinkTimer = 0f;
                _blinkVisible = false;
            }

            bool showLeft = _blinkVisible && (data.IsLeftTurn || data.IsHazard);
            bool showRight = _blinkVisible && (data.IsRightTurn || data.IsHazard);

            SetGroupActive(_leftTurnLights, showLeft);
            SetGroupActive(_rightTurnLights, showRight);

            // 3. Brake & Reverse Lights
            SetGroupActive(_brakeLights, data.IsBraking || data.IsHandbraking);
            SetGroupActive(_reverseLights, data.IsReversing);
            SetGroupActive(_handbrakeLights, data.IsHandbraking);

            // 4. Interior Dashboard Illumination
            SetGroupActive(_interiorLights, true);
        }

        private static bool AreStatesEqual(in VehicleLightsNetworkData a, in VehicleLightsNetworkData b)
        {
            return a.HeadlightMode == b.HeadlightMode &&
                   a.IsLeftTurn == b.IsLeftTurn &&
                   a.IsRightTurn == b.IsRightTurn &&
                   a.IsHazard == b.IsHazard &&
                   a.IsBraking == b.IsBraking &&
                   a.IsReversing == b.IsReversing &&
                   a.IsHandbraking == b.IsHandbraking;
        }

        private static void SetGroupActive(GameObject[] group, bool active)
        {
            if (group == null) return;
            for (int i = 0; i < group.Length; i++)
            {
                if (group[i] != null && group[i].activeSelf != active)
                {
                    group[i].SetActive(active);
                }
            }
        }

        private static void SetSpotlightsActive(Light[] spotlights, bool active)
        {
            if (spotlights == null) return;
            for (int i = 0; i < spotlights.Length; i++)
            {
                if (spotlights[i] != null && spotlights[i].enabled != active)
                {
                    spotlights[i].enabled = active;
                }
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
