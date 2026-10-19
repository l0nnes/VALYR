using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VALYR.Core;

namespace VALYR.Runtime.Visual
{
    public enum HeadlightMode
    {
        Off = 0,
        LowBeam = 1,
        HighBeam = 2
    }

    /// <summary>
    /// Vehicle lighting controller for VALYR vehicles.
    /// Manages low beam, high beam, tail running lights, automatic brake and reverse lights,
    /// turn signals and hazards, and interior illumination.
    /// Supports Unity New Input System actions and direct API calls.
    /// </summary>
    public class VehicleLightController : MonoBehaviour
    {
        [Header("Vehicle Reference")]
        [SerializeField] private VehicleController _controller;

        [Header("Headlights (Low Beam)")]
        [Tooltip("Low beam headlight mesh renderers or emissive objects.")]
        public GameObject[] LowBeamHeadlights;
        [Tooltip("Low beam spotlights (Unity Light components).")]
        public Light[] LowBeamSpotlights;

        [Header("Headlights (High Beam)")]
        [Tooltip("High beam headlight mesh renderers or emissive objects.")]
        public GameObject[] HighBeamHeadlights;
        [Tooltip("High beam spotlights (Unity Light components).")]
        public Light[] HighBeamSpotlights;

        [Header("Running Rear Lights")]
        [Tooltip("Rear tail light meshes illuminated when headlights are active.")]
        public GameObject[] RearRunningLights;

        [Header("Brake Lights")]
        [Tooltip("Brake light meshes illuminated automatically when brake pedal is pressed.")]
        public GameObject[] BrakeLights;

        [Header("Reverse Lights")]
        [Tooltip("Reverse light meshes illuminated automatically in reverse gear.")]
        public GameObject[] ReverseLights;

        [Header("Handbrake Indicator")]
        [Tooltip("Interior indicator or mesh illuminated when parking brake is on.")]
        public GameObject[] HandbrakeLights;

        [Header("Turn Signals")]
        [Tooltip("Left turn signal meshes (front, side, rear, interior indicator).")]
        public GameObject[] LeftTurnLights;
        [Tooltip("Right turn signal meshes (front, side, rear, interior indicator).")]
        public GameObject[] RightTurnLights;

        [Tooltip("Blink period in seconds (typical 0.35 - 0.45s).")]
        [Range(0.2f, 1.0f)]
        public float BlinkPeriod = 0.40f;

        [Header("Interior & Dashboard Lights")]
        [Tooltip("Interior and dashboard ambient illumination.")]
        public GameObject[] InteriorLights;

        [Header("Input Actions Asset")]
        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private string _actionMapName = "Driving";
        [SerializeField] private string _toggleHeadlightsAction = "Toggle Headlights";
        [SerializeField] private string _toggleHighBeamAction = "Toggle High Beam";
        [SerializeField] private string _leftTurnAction = "Left Turn Signal";
        [SerializeField] private string _rightTurnAction = "Right Turn Signal";
        [SerializeField] private string _hazardAction = "Hazard Lights";

        [Header("Audio (Optional)")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _turnSignalTickSound;

        // Current runtime states
        public HeadlightMode CurrentHeadlightMode { get; private set; } = HeadlightMode.Off;
        public bool IsLeftTurnActive { get; private set; }
        public bool IsRightTurnActive { get; private set; }
        public bool IsHazardActive { get; private set; }

        private float _blinkTimer;
        private bool _blinkVisible;

        private InputAction _headlightsActionRef;
        private InputAction _highBeamActionRef;
        private InputAction _leftTurnActionRef;
        private InputAction _rightTurnActionRef;
        private InputAction _hazardActionRef;

        private void Awake()
        {
            if (_controller == null)
            {
                _controller = GetComponent<VehicleController>() ?? GetComponentInParent<VehicleController>();
            }

            ResolveInputActions();
        }

        private void OnEnable()
        {
            SubscribeInputActions();
        }

        private void OnDisable()
        {
            UnsubscribeInputActions();
            AllLightsOff();
        }

        private void Start()
        {
            ApplyHeadlights();
            SetGroupActive(InteriorLights, true);
        }

        private void Update()
        {
            // 1. Process Turn Signal Blinking
            if (IsLeftTurnActive || IsRightTurnActive || IsHazardActive)
            {
                _blinkTimer += Time.deltaTime;
                if (_blinkTimer >= BlinkPeriod)
                {
                    _blinkTimer -= BlinkPeriod;
                    _blinkVisible = !_blinkVisible;

                    if (_blinkVisible && _audioSource != null && _turnSignalTickSound != null)
                    {
                        _audioSource.PlayOneShot(_turnSignalTickSound);
                    }
                }
            }
            else
            {
                _blinkTimer = 0f;
                _blinkVisible = false;
            }

            bool showLeft = _blinkVisible && (IsLeftTurnActive || IsHazardActive);
            bool showRight = _blinkVisible && (IsRightTurnActive || IsHazardActive);
            SetGroupActive(LeftTurnLights, showLeft);
            SetGroupActive(RightTurnLights, showRight);

            // 2. Dynamic Automatic Vehicle Lights
            if (_controller != null)
            {
                VehicleInput input = _controller.Input;
                VehicleState state = _controller.State;

                // Brake lights: on when brake pedal pressed, or in reverse when throttle acts as brake
                bool braking = (state.CurrentGear == -1) ? (input.Throttle > 0.05f) : (input.Brake > 0.05f);
                SetGroupActive(BrakeLights, braking);

                // Reverse lights: on when gear is reverse (-1)
                bool reversing = (state.CurrentGear == -1);
                SetGroupActive(ReverseLights, reversing);

                // Handbrake indicator: on when handbrake engaged
                bool handbraked = (input.Handbrake > 0.5f);
                SetGroupActive(HandbrakeLights, handbraked);
            }

            // 3. Fallback Keyboard Hotkeys (if Input Action asset not triggered)
            ProcessFallbackKeys();
        }

        private void ProcessFallbackKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (_headlightsActionRef == null && kb.lKey.wasPressedThisFrame)
            {
                CycleHeadlights();
            }
            if (_highBeamActionRef == null && kb.kKey.wasPressedThisFrame)
            {
                ToggleHighBeam();
            }
            if (_leftTurnActionRef == null && kb.qKey.wasPressedThisFrame)
            {
                ToggleLeftTurn();
            }
            if (_rightTurnActionRef == null && kb.eKey.wasPressedThisFrame)
            {
                ToggleRightTurn();
            }
            if (_hazardActionRef == null && kb.hKey.wasPressedThisFrame)
            {
                ToggleHazard();
            }
        }

        public void CycleHeadlights()
        {
            CurrentHeadlightMode = CurrentHeadlightMode switch
            {
                HeadlightMode.Off => HeadlightMode.LowBeam,
                HeadlightMode.LowBeam => HeadlightMode.HighBeam,
                HeadlightMode.HighBeam => HeadlightMode.Off,
                _ => HeadlightMode.Off
            };
            ApplyHeadlights();
        }

        public void ToggleHighBeam()
        {
            CurrentHeadlightMode = (CurrentHeadlightMode == HeadlightMode.HighBeam)
                ? HeadlightMode.LowBeam
                : HeadlightMode.HighBeam;
            ApplyHeadlights();
        }

        public void SetHeadlightMode(HeadlightMode mode)
        {
            CurrentHeadlightMode = mode;
            ApplyHeadlights();
        }

        public void ToggleLeftTurn()
        {
            IsHazardActive = false;
            IsRightTurnActive = false;
            IsLeftTurnActive = !IsLeftTurnActive;
            _blinkTimer = 0f;
            _blinkVisible = IsLeftTurnActive;
        }

        public void ToggleRightTurn()
        {
            IsHazardActive = false;
            IsLeftTurnActive = false;
            IsRightTurnActive = !IsRightTurnActive;
            _blinkTimer = 0f;
            _blinkVisible = IsRightTurnActive;
        }

        public void ToggleHazard()
        {
            IsLeftTurnActive = false;
            IsRightTurnActive = false;
            IsHazardActive = !IsHazardActive;
            _blinkTimer = 0f;
            _blinkVisible = IsHazardActive;
        }

        public void AllLightsOff()
        {
            CurrentHeadlightMode = HeadlightMode.Off;
            ApplyHeadlights();
            IsLeftTurnActive = false;
            IsRightTurnActive = false;
            IsHazardActive = false;
            SetGroupActive(LeftTurnLights, false);
            SetGroupActive(RightTurnLights, false);
            SetGroupActive(BrakeLights, false);
            SetGroupActive(ReverseLights, false);
            SetGroupActive(HandbrakeLights, false);
        }

        private void ApplyHeadlights()
        {
            bool hasLow = CurrentHeadlightMode == HeadlightMode.LowBeam || CurrentHeadlightMode == HeadlightMode.HighBeam;
            bool hasHigh = CurrentHeadlightMode == HeadlightMode.HighBeam;

            SetGroupActive(LowBeamHeadlights, hasLow);
            SetSpotlightsActive(LowBeamSpotlights, CurrentHeadlightMode == HeadlightMode.LowBeam);

            SetGroupActive(HighBeamHeadlights, hasHigh);
            SetSpotlightsActive(HighBeamSpotlights, hasHigh);

            SetGroupActive(RearRunningLights, hasLow);
        }

        private void SetGroupActive(GameObject[] group, bool active)
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

        private void SetSpotlightsActive(Light[] spotlights, bool active)
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

        private void ResolveInputActions()
        {
            if (_inputActions == null)
            {
                var allAssets = Resources.FindObjectsOfTypeAll<InputActionAsset>();
                for (int i = 0; i < allAssets.Length; i++)
                {
                    if (allAssets[i] != null && allAssets[i].name == "GameInput")
                    {
                        _inputActions = allAssets[i];
                        break;
                    }
                }
            }

            if (_inputActions != null)
            {
                var map = _inputActions.FindActionMap(_actionMapName);
                if (map != null)
                {
                    _headlightsActionRef = map.FindAction(_toggleHeadlightsAction);
                    _highBeamActionRef = map.FindAction(_toggleHighBeamAction);
                    _leftTurnActionRef = map.FindAction(_leftTurnAction);
                    _rightTurnActionRef = map.FindAction(_rightTurnAction);
                    _hazardActionRef = map.FindAction(_hazardAction);
                }
            }
        }

        private void SubscribeInputActions()
        {
            if (_headlightsActionRef != null) _headlightsActionRef.performed += OnToggleHeadlightsInput;
            if (_highBeamActionRef != null) _highBeamActionRef.performed += OnToggleHighBeamInput;
            if (_leftTurnActionRef != null) _leftTurnActionRef.performed += OnLeftTurnInput;
            if (_rightTurnActionRef != null) _rightTurnActionRef.performed += OnRightTurnInput;
            if (_hazardActionRef != null) _hazardActionRef.performed += OnHazardInput;
        }

        private void UnsubscribeInputActions()
        {
            if (_headlightsActionRef != null) _headlightsActionRef.performed -= OnToggleHeadlightsInput;
            if (_highBeamActionRef != null) _highBeamActionRef.performed -= OnToggleHighBeamInput;
            if (_leftTurnActionRef != null) _leftTurnActionRef.performed -= OnLeftTurnInput;
            if (_rightTurnActionRef != null) _rightTurnActionRef.performed -= OnRightTurnInput;
            if (_hazardActionRef != null) _hazardActionRef.performed -= OnHazardInput;
        }

        private void OnToggleHeadlightsInput(InputAction.CallbackContext ctx) => CycleHeadlights();
        private void OnToggleHighBeamInput(InputAction.CallbackContext ctx) => ToggleHighBeam();
        private void OnLeftTurnInput(InputAction.CallbackContext ctx) => ToggleLeftTurn();
        private void OnRightTurnInput(InputAction.CallbackContext ctx) => ToggleRightTurn();
        private void OnHazardInput(InputAction.CallbackContext ctx) => ToggleHazard();
    }
}
