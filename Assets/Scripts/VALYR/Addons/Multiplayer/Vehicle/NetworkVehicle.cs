using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Unity.Cinemachine;
using PurrNet;
using VALYR.Core;
using VALYR.Runtime;
using VALYR.Runtime.Input;
using VALYR.Runtime.Visual;
using VALYR.Runtime.Cameras;
using VALYR.Runtime.UI;

namespace VALYR.Multiplayer
{
    /// <summary>
    /// Multiplayer addon adapter for VALYR vehicle.
    /// Manages client-owner authority, disables remote inputs/cameras,
    /// synchronizes wheel transforms, lighting, and door states over PurrNet,
    /// dynamically binds the local player's Cinemachine camera rig with all camera slots,
    /// and configures rigidbodies for dynamic PhysX collisions without gravity belly sagging.
    /// </summary>
    [RequireComponent(typeof(NetworkRigidbody))]
    [RequireComponent(typeof(VehicleController))]
    public class NetworkVehicle : NetworkBehaviour
    {
        [Header("PurrNet State Replication")]
        [Tooltip("Owner-authoritative synchronized driving and wheel visuals state.")]
        [SerializeField]
        private SyncVar<VehicleNetworkState> _networkState = new(default, 0.033f, ownerAuth: true);

        // Cached vehicle components
        private VehicleController _controller;
        private VehicleInputReader _inputReader;
        private VehicleCameraController _cameraController;
        private VehicleTelemetryHUD _telemetryHUD;
        private VehicleVisualView _visualView;
        private Component _doorController;
        private Component _lightController;
        private NetworkRigidbody _networkRigidbody;
        private Rigidbody _rigidbody;

        private static readonly FieldInfo CameraSlotsField =
            typeof(VehicleCameraController).GetField("_cameraSlots", BindingFlags.NonPublic | BindingFlags.Instance);

        // Reflection cache for VehicleDoorController & VehicleLightController (Assembly-CSharp)
        private static readonly Type DoorControllerType =
            Type.GetType("VALYR.Runtime.Visual.VehicleDoorController, Assembly-CSharp");
        private static readonly FieldInfo LeftDoorField = DoorControllerType?.GetField("LeftDoor");
        private static readonly FieldInfo RightDoorField = DoorControllerType?.GetField("RightDoor");
        private static readonly FieldInfo HoodField = DoorControllerType?.GetField("Hood");
        private static readonly FieldInfo TailgateField = DoorControllerType?.GetField("Tailgate");
        private static readonly FieldInfo IsOpenField = LeftDoorField?.FieldType.GetField("IsOpen");
        private static readonly MethodInfo DoorToggleLeftMethod = DoorControllerType?.GetMethod("ToggleLeftDoor");
        private static readonly MethodInfo DoorToggleRightMethod = DoorControllerType?.GetMethod("ToggleRightDoor");
        private static readonly MethodInfo DoorToggleHoodMethod = DoorControllerType?.GetMethod("ToggleHood");
        private static readonly MethodInfo DoorToggleTailgateMethod = DoorControllerType?.GetMethod("ToggleTailgate");
        private static readonly MethodInfo DoorResolveInputMethod =
            DoorControllerType?.GetMethod("ResolveInputActions", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo DoorSubscribeMethod =
            DoorControllerType?.GetMethod("SubscribeInputActions", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo DoorUnsubscribeMethod =
            DoorControllerType?.GetMethod("UnsubscribeInputActions", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly Type LightControllerType =
            Type.GetType("VALYR.Runtime.Visual.VehicleLightController, Assembly-CSharp");
        private static readonly PropertyInfo CurrentHeadlightModeProp = LightControllerType?.GetProperty("CurrentHeadlightMode");
        private static readonly PropertyInfo IsHazardActiveProp = LightControllerType?.GetProperty("IsHazardActive");
        private static readonly PropertyInfo IsLeftTurnActiveProp = LightControllerType?.GetProperty("IsLeftTurnActive");
        private static readonly PropertyInfo IsRightTurnActiveProp = LightControllerType?.GetProperty("IsRightTurnActive");
        private static readonly MethodInfo SetHeadlightModeMethod = LightControllerType?.GetMethod("SetHeadlightMode");
        private static readonly MethodInfo LightCycleHeadlightsMethod = LightControllerType?.GetMethod("CycleHeadlights");
        private static readonly MethodInfo LightToggleHighBeamMethod = LightControllerType?.GetMethod("ToggleHighBeam");
        private static readonly MethodInfo LightToggleLeftTurnMethod = LightControllerType?.GetMethod("ToggleLeftTurn");
        private static readonly MethodInfo LightToggleRightTurnMethod = LightControllerType?.GetMethod("ToggleRightTurn");
        private static readonly MethodInfo LightToggleHazardMethod = LightControllerType?.GetMethod("ToggleHazard");
        private static readonly FieldInfo BrakeLightsField = LightControllerType?.GetField("BrakeLights");
        private static readonly FieldInfo ReverseLightsField = LightControllerType?.GetField("ReverseLights");
        private static readonly FieldInfo HandbrakeLightsField = LightControllerType?.GetField("HandbrakeLights");
        private static readonly MethodInfo LightResolveInputMethod =
            LightControllerType?.GetMethod("ResolveInputActions", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo LightSubscribeMethod =
            LightControllerType?.GetMethod("SubscribeInputActions", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo LightUnsubscribeMethod =
            LightControllerType?.GetMethod("UnsubscribeInputActions", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>
        /// Explicit target player ID assigned during server spawn.
        /// Prevents host from adopting remote client vehicles as local during spawn.
        /// </summary>
        public PlayerID? AssignedPlayerId { get; set; }

        public bool IsLocallyControlled
        {
            get
            {
                if (NetworkManager.main == null || NetworkManager.main.isOffline)
                {
                    return true;
                }

                if (!isSpawned)
                {
                    return false;
                }

                // If explicitly assigned during spawn, verify against local player ID so server/host
                // doesn't claim client car before ownership replication
                if (AssignedPlayerId.HasValue)
                {
                    return AssignedPlayerId.Value == NetworkManager.main.localPlayer;
                }

                return isOwner;
            }
        }

        public VehicleNetworkState SyncedState => _networkState.value;

        private void Awake()
        {
            EnsureComponentsCached();
        }

        private void EnsureComponentsCached()
        {
            if (_controller == null) _controller = GetComponent<VehicleController>();
            if (_inputReader == null) _inputReader = GetComponent<VehicleInputReader>();
            if (_cameraController == null) _cameraController = GetComponent<VehicleCameraController>();
            if (_telemetryHUD == null) _telemetryHUD = GetComponent<VehicleTelemetryHUD>();
            if (_visualView == null) _visualView = GetComponent<VehicleVisualView>();
            if (_networkRigidbody == null) _networkRigidbody = GetComponent<NetworkRigidbody>();
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();

            if (_doorController == null && DoorControllerType != null)
                _doorController = GetComponent(DoorControllerType);
            if (_lightController == null && LightControllerType != null)
                _lightController = GetComponent(LightControllerType);
        }

        private void Start()
        {
            UpdateAuthorityState();
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();
            UpdateAuthorityState();
        }

        protected override void OnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer)
        {
            base.OnOwnerChanged(oldOwner, newOwner, asServer);
            UpdateAuthorityState();
        }

        /// <summary>
        /// Configures vehicle systems depending on whether this machine is the driver (Owner) or observer (Remote).
        /// </summary>
        public void UpdateAuthorityState()
        {
            EnsureComponentsCached();
            bool localControl = IsLocallyControlled;

            // 1. Vehicle driving physics & input
            if (_controller != null)
                _controller.enabled = localControl;

            if (_inputReader != null)
            {
                if (!localControl)
                {
                    // Prevent VehicleInputReader.OnDisable() from disabling the shared InputActionMap for the local player!
                    SetPrivateField(_inputReader, "_actionMap", null);
                }
                _inputReader.enabled = localControl;
                if (localControl)
                {
                    // Ensure local input reader action map is initialized and enabled
                    typeof(VehicleInputReader).GetMethod("InitializeInputActions",
                        BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(_inputReader, null);
                    var map = _inputReader.GetType().GetField("_actionMap", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(_inputReader) as UnityEngine.InputSystem.InputActionMap;
                    if (map != null && !map.enabled)
                    {
                        map.Enable();
                    }
                }
            }

            // 2. Camera controller & Cinemachine bindings
            if (_cameraController != null)
            {
                if (!localControl)
                {
                    // Prevent VehicleCameraController.OnDisable() from disabling the shared InputActionMap for the local player!
                    SetPrivateField(_cameraController, "_actionMap", null);
                }
                _cameraController.enabled = localControl;
                if (localControl)
                {
                    RebindCinemachineCameras();
                }
            }

            // 3. UI HUD
            if (_telemetryHUD != null)
                _telemetryHUD.enabled = localControl;

            // 4. Doors & Lights Input isolation
            ConfigureDoorAndLightInput(localControl);

            // 5. Rigidbody physics configuration
            if (_rigidbody != null)
            {
                // Always dynamic so PhysX collision impulses between vehicles work realistically!
                _rigidbody.isKinematic = false;
                _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

                if (localControl)
                {
                    // Local driver: full gravity supported by local suspension raycasts
                    _rigidbody.useGravity = true;
                }
                else
                {
                    // Remote proxy: zero gravity to prevent sagging while keeping full dynamic collision responses!
                    _rigidbody.useGravity = false;
                }
            }
        }

        private void ConfigureDoorAndLightInput(bool localControl)
        {
            if (_doorController != null)
            {
                DoorUnsubscribeMethod?.Invoke(_doorController, null);
                if (!localControl)
                {
                    // Assign dummy action reference so ProcessFallbackKeys() doesn't respond to local player keys (1, 2, 3, 4)
                    var dummyAction = new UnityEngine.InputSystem.InputAction("ProxyDoorDummy");
                    SetPrivateField(_doorController, "_leftDoorActionRef", dummyAction);
                    SetPrivateField(_doorController, "_rightDoorActionRef", dummyAction);
                    SetPrivateField(_doorController, "_hoodActionRef", dummyAction);
                    SetPrivateField(_doorController, "_tailgateActionRef", dummyAction);
                }
                else
                {
                    SetPrivateField(_doorController, "_leftDoorActionRef", null);
                    SetPrivateField(_doorController, "_rightDoorActionRef", null);
                    SetPrivateField(_doorController, "_hoodActionRef", null);
                    SetPrivateField(_doorController, "_tailgateActionRef", null);
                    DoorResolveInputMethod?.Invoke(_doorController, null);
                    DoorSubscribeMethod?.Invoke(_doorController, null);
                }
            }

            if (_lightController != null)
            {
                LightUnsubscribeMethod?.Invoke(_lightController, null);
                if (!localControl)
                {
                    var dummyAction = new UnityEngine.InputSystem.InputAction("ProxyLightDummy");
                    SetPrivateField(_lightController, "_headlightsActionRef", dummyAction);
                    SetPrivateField(_lightController, "_highBeamActionRef", dummyAction);
                    SetPrivateField(_lightController, "_leftTurnActionRef", dummyAction);
                    SetPrivateField(_lightController, "_rightTurnActionRef", dummyAction);
                    SetPrivateField(_lightController, "_hazardActionRef", dummyAction);
                }
                else
                {
                    SetPrivateField(_lightController, "_headlightsActionRef", null);
                    SetPrivateField(_lightController, "_highBeamActionRef", null);
                    SetPrivateField(_lightController, "_leftTurnActionRef", null);
                    SetPrivateField(_lightController, "_rightTurnActionRef", null);
                    SetPrivateField(_lightController, "_hazardActionRef", null);
                    LightResolveInputMethod?.Invoke(_lightController, null);
                    LightSubscribeMethod?.Invoke(_lightController, null);
                }
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(target, value);
        }

        private byte GetDoorFlags()
        {
            if (_doorController == null || DoorControllerType == null || IsOpenField == null)
                return 0;

            byte flags = 0;
            if (GetDoorIsOpen(LeftDoorField)) flags |= 1 << 0;
            if (GetDoorIsOpen(RightDoorField)) flags |= 1 << 1;
            if (GetDoorIsOpen(HoodField)) flags |= 1 << 2;
            if (GetDoorIsOpen(TailgateField)) flags |= 1 << 3;
            return flags;
        }

        private bool GetDoorIsOpen(FieldInfo doorField)
        {
            if (doorField == null) return false;
            var slot = doorField.GetValue(_doorController);
            if (slot == null) return false;
            return (bool)IsOpenField.GetValue(slot);
        }

        private void SetDoorIsOpen(FieldInfo doorField, bool open)
        {
            if (doorField == null) return;
            var slot = doorField.GetValue(_doorController);
            if (slot == null) return;
            IsOpenField.SetValue(slot, open);
        }

        private void FixedUpdate()
        {
            // Owner writes local physics simulation snapshot to SyncVar
            if (isSpawned && isOwner && _controller != null)
            {
                VehicleState state = _controller.State;
                VehicleInput input = _controller.Input;

                byte headlightMode = 0;
                bool isHazard = false;
                bool isLeftTurn = false;
                bool isRightTurn = false;

                if (_lightController != null)
                {
                    if (CurrentHeadlightModeProp != null)
                        headlightMode = Convert.ToByte(CurrentHeadlightModeProp.GetValue(_lightController));
                    if (IsHazardActiveProp != null)
                        isHazard = (bool)IsHazardActiveProp.GetValue(_lightController);
                    if (IsLeftTurnActiveProp != null)
                        isLeftTurn = (bool)IsLeftTurnActiveProp.GetValue(_lightController);
                    if (IsRightTurnActiveProp != null)
                        isRightTurn = (bool)IsRightTurnActiveProp.GetValue(_lightController);
                }

                byte doorFlags = GetDoorFlags();

                var net = new VehicleNetworkState
                {
                    EngineRpm = state.EngineRpm,
                    CurrentGear = state.CurrentGear,
                    SteerAngle = state.Wheels[0].SteerAngle,
                    SuspLength0 = state.Wheels[0].SuspensionLength,
                    SuspLength1 = state.Wheels[1].SuspensionLength,
                    SuspLength2 = state.Wheels[2].SuspensionLength,
                    SuspLength3 = state.Wheels[3].SuspensionLength,
                    RollAngle0 = state.Wheels[0].RotationAngle,
                    RollAngle1 = state.Wheels[1].RotationAngle,
                    RollAngle2 = state.Wheels[2].RotationAngle,
                    RollAngle3 = state.Wheels[3].RotationAngle,
                    IsBraking = input.Brake > 0.05f || (state.CurrentGear == -1 && input.Throttle > 0.05f),
                    IsHandbraking = input.Handbrake > 0.5f,
                    IsReversing = state.CurrentGear < 0,
                    HeadlightMode = headlightMode,
                    IsHazardActive = isHazard,
                    IsLeftTurnActive = isLeftTurn,
                    IsRightTurnActive = isRightTurn,
                    DoorOpenFlags = doorFlags
                };

                _networkState.value = net;
            }
        }

        private void Update()
        {
            // Direct keyboard input fallback for locally controlled driver (guarantees inputs work seamlessly per client)
            if (IsLocallyControlled)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null)
                {
                    // Camera switching (C key)
                    if (kb.cKey.wasPressedThisFrame && _cameraController != null && _cameraController.enabled)
                    {
                        _cameraController.CycleNextCamera();
                    }

                    // Door controls (1, 2, 3, 4 keys)
                    if (_doorController != null)
                    {
                        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
                            DoorToggleLeftMethod?.Invoke(_doorController, null);
                        if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
                            DoorToggleRightMethod?.Invoke(_doorController, null);
                        if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
                            DoorToggleHoodMethod?.Invoke(_doorController, null);
                        if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame)
                            DoorToggleTailgateMethod?.Invoke(_doorController, null);
                    }

                    // Light controls (L, K, Q, E, H keys)
                    if (_lightController != null)
                    {
                        if (kb.lKey.wasPressedThisFrame)
                            LightCycleHeadlightsMethod?.Invoke(_lightController, null);
                        if (kb.kKey.wasPressedThisFrame)
                            LightToggleHighBeamMethod?.Invoke(_lightController, null);
                        if (kb.qKey.wasPressedThisFrame)
                            LightToggleLeftTurnMethod?.Invoke(_lightController, null);
                        if (kb.eKey.wasPressedThisFrame)
                            LightToggleRightTurnMethod?.Invoke(_lightController, null);
                        if (kb.hKey.wasPressedThisFrame)
                            LightToggleHazardMethod?.Invoke(_lightController, null);
                    }
                }
            }

            // Remote proxies animate wheels, doors, and lights using received network state
            if (isSpawned && !isOwner)
            {
                VehicleNetworkState net = _networkState.value;

                // 1. Wheel visuals & steering
                if (_visualView != null)
                {
                    VehicleState proxyState = default;
                    proxyState.EngineRpm = net.EngineRpm;
                    proxyState.CurrentGear = net.CurrentGear;

                    proxyState.Wheels.FL.SteerAngle = net.SteerAngle;
                    proxyState.Wheels.FR.SteerAngle = net.SteerAngle;
                    proxyState.Wheels.RL.SteerAngle = 0f;
                    proxyState.Wheels.RR.SteerAngle = 0f;

                    // Fallback to nominal suspension length (0.38f) if not yet received from network
                    proxyState.Wheels.FL.SuspensionLength = net.SuspLength0 > 0.001f ? net.SuspLength0 : 0.38f;
                    proxyState.Wheels.FR.SuspensionLength = net.SuspLength1 > 0.001f ? net.SuspLength1 : 0.38f;
                    proxyState.Wheels.RL.SuspensionLength = net.SuspLength2 > 0.001f ? net.SuspLength2 : 0.38f;
                    proxyState.Wheels.RR.SuspensionLength = net.SuspLength3 > 0.001f ? net.SuspLength3 : 0.38f;

                    proxyState.Wheels.FL.RotationAngle = net.RollAngle0;
                    proxyState.Wheels.FR.RotationAngle = net.RollAngle1;
                    proxyState.Wheels.RL.RotationAngle = net.RollAngle2;
                    proxyState.Wheels.RR.RotationAngle = net.RollAngle3;

                    _visualView.UpdateWheelsView(in proxyState, Time.deltaTime);
                }

                // 2. Door visuals
                if (_doorController != null && IsOpenField != null)
                {
                    SetDoorIsOpen(LeftDoorField, (net.DoorOpenFlags & (1 << 0)) != 0);
                    SetDoorIsOpen(RightDoorField, (net.DoorOpenFlags & (1 << 1)) != 0);
                    SetDoorIsOpen(HoodField, (net.DoorOpenFlags & (1 << 2)) != 0);
                    SetDoorIsOpen(TailgateField, (net.DoorOpenFlags & (1 << 3)) != 0);
                }

                // 3. Headlight mode
                if (_lightController != null && SetHeadlightModeMethod != null && CurrentHeadlightModeProp != null)
                {
                    byte currentMode = Convert.ToByte(CurrentHeadlightModeProp.GetValue(_lightController));
                    if (currentMode != net.HeadlightMode)
                    {
                        var paramType = SetHeadlightModeMethod.GetParameters()[0].ParameterType;
                        var enumVal = Enum.ToObject(paramType, net.HeadlightMode);
                        SetHeadlightModeMethod.Invoke(_lightController, new[] { enumVal });
                    }
                }
            }
        }

        private void LateUpdate()
        {
            // Apply brake, reverse, and handbrake visual lights on remote proxies in LateUpdate
            if (isSpawned && !isOwner && _lightController != null)
            {
                VehicleNetworkState net = _networkState.value;
                if (BrakeLightsField != null)
                    SetGroupActive(BrakeLightsField.GetValue(_lightController) as GameObject[], net.IsBraking || net.IsHandbraking);
                if (ReverseLightsField != null)
                    SetGroupActive(ReverseLightsField.GetValue(_lightController) as GameObject[], net.IsReversing);
                if (HandbrakeLightsField != null)
                    SetGroupActive(HandbrakeLightsField.GetValue(_lightController) as GameObject[], net.IsHandbraking);
            }
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

        /// <summary>
        /// Binds the scene Cinemachine cameras to follow this vehicle for the local player,
        /// initializes input actions, and configures all camera slots so camera switching (C key) functions properly.
        /// </summary>
        public void RebindCinemachineCameras()
        {
            EnsureComponentsCached();
            Transform lookTarget = transform.Find("LookTarget") ?? transform;
            Transform cockpit = transform.Find("CameraAnchors/CockpitAnchor") ?? transform.Find("CockpitAnchor");
            Transform bumper = transform.Find("CameraAnchors/BumperAnchor") ?? transform.Find("BumperAnchor");

            CinemachineCamera chaseNear = null;
            CinemachineCamera chaseFar = null;
            CinemachineCamera cockpitCam = null;
            CinemachineCamera bumperCam = null;
            CinemachineCamera freeLook = null;

            var virtualCameras = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include);
            for (int i = 0; i < virtualCameras.Length; i++)
            {
                var vcam = virtualCameras[i];
                if (vcam == null) continue;

                string vcamName = vcam.name.ToLowerInvariant();

                if (vcamName.Contains("cockpit") || vcamName.Contains("interior"))
                {
                    cockpitCam = vcam;
                    vcam.Target.TrackingTarget = cockpit != null ? cockpit : transform;
                    vcam.Target.LookAtTarget = null;
                }
                else if (vcamName.Contains("bumper") || vcamName.Contains("hood"))
                {
                    bumperCam = vcam;
                    vcam.Target.TrackingTarget = bumper != null ? bumper : transform;
                    vcam.Target.LookAtTarget = null;
                }
                else if (vcamName.Contains("far"))
                {
                    chaseFar = vcam;
                    vcam.Target.TrackingTarget = transform;
                    vcam.Target.LookAtTarget = lookTarget;
                }
                else if (vcamName.Contains("orbit") || vcamName.Contains("free"))
                {
                    freeLook = vcam;
                    vcam.Target.TrackingTarget = transform;
                    vcam.Target.LookAtTarget = lookTarget;
                }
                else if (vcamName.Contains("near") || vcamName.Contains("chase"))
                {
                    chaseNear = vcam;
                    vcam.Target.TrackingTarget = transform;
                    vcam.Target.LookAtTarget = lookTarget;
                }
            }

            // Populate _cameraSlots on VehicleCameraController so "Change Camera" (C key) works seamlessly
            if (_cameraController != null)
            {
                // Ensure Input Actions are initialized and action map enabled on camera controller
                typeof(VehicleCameraController).GetMethod("InitializeInputActions",
                    BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(_cameraController, null);

                var camMap = _cameraController.GetType().GetField("_actionMap", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(_cameraController) as UnityEngine.InputSystem.InputActionMap;
                if (camMap != null && !camMap.enabled)
                {
                    camMap.Enable();
                }

                if (CameraSlotsField != null)
                {
                    var slots = CameraSlotsField.GetValue(_cameraController) as List<VehicleCameraSlot>;
                    if (slots == null)
                    {
                        slots = new List<VehicleCameraSlot>();
                        CameraSlotsField.SetValue(_cameraController, slots);
                    }

                    slots.Clear();

                    if (chaseNear != null)
                        slots.Add(new VehicleCameraSlot { Mode = VehicleCameraMode.ChaseNear, DisplayName = "Chase Near", Camera = chaseNear, BaseFOV = 60f, MaxFOV = 74f, EnableDynamicFOV = true });
                    if (chaseFar != null)
                        slots.Add(new VehicleCameraSlot { Mode = VehicleCameraMode.ChaseFar, DisplayName = "Chase Far", Camera = chaseFar, BaseFOV = 65f, MaxFOV = 78f, EnableDynamicFOV = true });
                    if (cockpitCam != null)
                        slots.Add(new VehicleCameraSlot { Mode = VehicleCameraMode.Cockpit, DisplayName = "Cockpit", Camera = cockpitCam, BaseFOV = 75f, MaxFOV = 80f, EnableDynamicFOV = true });
                    if (bumperCam != null)
                        slots.Add(new VehicleCameraSlot { Mode = VehicleCameraMode.Bumper, DisplayName = "Bumper / Hood", Camera = bumperCam, BaseFOV = 75f, MaxFOV = 85f, EnableDynamicFOV = true });
                    if (freeLook != null)
                        slots.Add(new VehicleCameraSlot { Mode = VehicleCameraMode.FreeOrbit, DisplayName = "Free Orbit", Camera = freeLook, BaseFOV = 60f, MaxFOV = 60f, EnableDynamicFOV = false });

                    _cameraController.SetCameraMode(VehicleCameraMode.ChaseNear);
                }
            }
        }
    }
}
