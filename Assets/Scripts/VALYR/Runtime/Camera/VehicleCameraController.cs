using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

namespace VALYR.Runtime.Cameras
{
    /// <summary>
    /// Available vehicle camera modes.
    /// </summary>
    public enum VehicleCameraMode
    {
        ChaseNear = 0,
        ChaseFar = 1,
        Cockpit = 2,
        Bumper = 3,
        FreeOrbit = 4
    }

    /// <summary>
    /// Definition of a single vehicle virtual camera configuration.
    /// </summary>
    [Serializable]
    public class VehicleCameraSlot
    {
        [Tooltip("Camera mode represented by this slot.")]
        public VehicleCameraMode Mode;

        [Tooltip("Display name shown on UI/HUD.")]
        public string DisplayName;

        [Tooltip("The CinemachineCamera instance.")]
        public CinemachineCamera Camera;

        [Tooltip("Base field of view at standstill.")]
        [Range(30f, 100f)]
        public float BaseFOV = 60f;

        [Tooltip("Maximum field of view at high speed.")]
        [Range(30f, 120f)]
        public float MaxFOV = 74f;

        [Tooltip("Enable dynamic speed-based FOV expansion for this camera.")]
        public bool EnableDynamicFOV = true;
    }

    /// <summary>
    /// Comprehensive Cinemachine-based vehicle camera system with New Input System support.
    /// Supports multiple camera modes (Chase Near, Chase Far, Cockpit, Bumper, Free Orbit),
    /// dynamic speed FOV expansion, manual look-around with auto-recentering, and quick Look Back.
    /// </summary>
    public class VehicleCameraController : MonoBehaviour
    {
        [Header("Target Vehicle")]
        [Tooltip("Reference to the VehicleController or Rigidbody. If null, will search on parent or self.")]
        [SerializeField] private VehicleController _vehicle;

        [Tooltip("Transform representing the vehicle center/aim target.")]
        [SerializeField] private Transform _lookTarget;

        [Tooltip("Transform representing cockpit / driver eye position.")]
        [SerializeField] private Transform _cockpitAnchor;

        [Tooltip("Transform representing front bumper position.")]
        [SerializeField] private Transform _bumperAnchor;

        [Header("Camera Rig & Slots")]
        [Tooltip("List of camera slots managed by this controller.")]
        [SerializeField] private List<VehicleCameraSlot> _cameraSlots = new();

        [Tooltip("Index of the active camera mode.")]
        [SerializeField] private int _currentSlotIndex = 0;

        [Header("Cinemachine Priority Settings")]
        [Tooltip("Priority assigned to the active Cinemachine camera.")]
        public int ActivePriority = 20;

        [Tooltip("Priority assigned to inactive Cinemachine cameras.")]
        public int InactivePriority = 10;

        [Header("Dynamic Speed FOV")]
        [Tooltip("Vehicle speed (km/h) at which maximum FOV expansion is reached.")]
        public float MaxFovSpeedKmh = 160f;

        [Tooltip("FOV smoothing rate (1/s).")]
        public float FovLerpSpeed = 4f;

        [Header("Chase Orbit & Auto-Recentering")]
        [Tooltip("Manual look orbit sensitivity multiplier (X = yaw, Y = pitch).")]
        public Vector2 LookSensitivity = new Vector2(1.5f, 1.2f);

        [Tooltip("Pitch clamp limits in degrees (min, max).")]
        public Vector2 PitchLimits = new Vector2(-15f, 50f);

        [Tooltip("Seconds of no look input before the chase camera auto-recenters behind the moving car.")]
        public float RecenterDelay = 1.2f;

        [Tooltip("Speed (1/s) at which the camera recenters behind the car.")]
        public float RecenterSpeed = 4.0f;

        [Tooltip("Minimum vehicle speed (km/h) required for auto-recentering to activate.")]
        public float MinRecenterSpeedKmh = 5f;

        [Header("Input Actions Asset")]
        [Tooltip("Input Actions asset (e.g. GameInput). Can be assigned in Inspector or loaded automatically.")]
        [SerializeField] private InputActionAsset _inputActions;

        [Tooltip("Action map name.")]
        [SerializeField] private string _actionMapName = "Driving";

        [Tooltip("Action name to cycle between camera views.")]
        [SerializeField] private string _changeCameraActionName = "Change Camera";

        [Tooltip("Action name to glance / look backwards.")]
        [SerializeField] private string _lookBackActionName = "Look Back";

        [Tooltip("Vector2 action name for manual look around.")]
        [SerializeField] private string _lookActionName = "Look";

        // Resolved Input Actions
        private InputActionMap _actionMap;
        private InputAction _changeCameraAction;
        private InputAction _lookBackAction;
        private InputAction _lookAction;

        // Internal orbit and state tracking
        private float _orbitYaw;
        private float _orbitPitch;
        private float _lastLookInputTime;
        private bool _isLookingBack;
        private float _currentSpeedKmh;

        public VehicleCameraSlot CurrentSlot => (_cameraSlots != null && _cameraSlots.Count > _currentSlotIndex && _currentSlotIndex >= 0)
            ? _cameraSlots[_currentSlotIndex]
            : null;

        public string CurrentModeName => CurrentSlot != null ? CurrentSlot.DisplayName : "None";
        public float CurrentSpeedKmh => _currentSpeedKmh;
        public bool IsLookingBack => _isLookingBack;

        public event Action<VehicleCameraSlot> OnCameraChanged;

        private void Awake()
        {
            if (_vehicle == null)
            {
                _vehicle = GetComponentInParent<VehicleController>() ?? GetComponent<VehicleController>();
            }

            InitializeInputActions();
        }

        private void Start()
        {
            EnsureCameraSlots();
            ApplyCameraPriorities();
        }

        private void OnEnable()
        {
            if (_actionMap != null && !_actionMap.enabled)
            {
                _actionMap.Enable();
            }

            if (_changeCameraAction != null)
            {
                _changeCameraAction.performed += OnChangeCameraInput;
            }
        }

        private void OnDisable()
        {
            if (_changeCameraAction != null)
            {
                _changeCameraAction.performed -= OnChangeCameraInput;
            }

            _actionMap?.Disable();
        }

        private void InitializeInputActions()
        {
            if (_inputActions == null)
            {
#if UNITY_EDITOR
                _inputActions = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/GameInput.inputactions");
#endif
            }

            if (_inputActions != null)
            {
                _actionMap = _inputActions.FindActionMap(_actionMapName);
                if (_actionMap != null)
                {
                    _changeCameraAction = _actionMap.FindAction(_changeCameraActionName);
                    _lookBackAction = _actionMap.FindAction(_lookBackActionName);
                    _lookAction = _actionMap.FindAction(_lookActionName);

                    if (isActiveAndEnabled)
                    {
                        _actionMap.Enable();
                    }
                }
            }
        }

        private void OnChangeCameraInput(InputAction.CallbackContext ctx)
        {
            CycleNextCamera();
        }

        /// <summary>
        /// Cycles to the next available camera slot.
        /// </summary>
        public void CycleNextCamera()
        {
            if (_cameraSlots == null || _cameraSlots.Count <= 1)
                return;

            _currentSlotIndex = (_currentSlotIndex + 1) % _cameraSlots.Count;
            ApplyCameraPriorities();
            OnCameraChanged?.Invoke(CurrentSlot);
        }

        /// <summary>
        /// Selects a specific camera mode directly.
        /// </summary>
        public void SetCameraMode(VehicleCameraMode mode)
        {
            if (_cameraSlots == null) return;

            for (int i = 0; i < _cameraSlots.Count; i++)
            {
                if (_cameraSlots[i].Mode == mode)
                {
                    _currentSlotIndex = i;
                    ApplyCameraPriorities();
                    OnCameraChanged?.Invoke(CurrentSlot);
                    return;
                }
            }
        }

        private void ApplyCameraPriorities()
        {
            if (_cameraSlots == null) return;

            for (int i = 0; i < _cameraSlots.Count; i++)
            {
                var slot = _cameraSlots[i];
                if (slot != null && slot.Camera != null)
                {
                    slot.Camera.Priority = (i == _currentSlotIndex) ? ActivePriority : InactivePriority;
                }
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // 1. Calculate vehicle speed
            if (_vehicle != null && _vehicle.Rigidbody != null)
            {
                Vector3 vel = _vehicle.Rigidbody.linearVelocity;
                float forwardSpeed = Vector3.Dot(vel, _vehicle.transform.forward);
                _currentSpeedKmh = Mathf.Abs(forwardSpeed) * 3.6f;
            }

            // 2. Read Look Back input
            bool lookBackHeld = false;
            if (_lookBackAction != null)
            {
                lookBackHeld = _lookBackAction.IsPressed();
            }
            else if (Keyboard.current != null && Keyboard.current.bKey.isPressed)
            {
                lookBackHeld = true;
            }
            _isLookingBack = lookBackHeld;

            // 3. Read manual look/orbit input
            Vector2 lookInput = Vector2.zero;
            if (_lookAction != null)
            {
                lookInput = _lookAction.ReadValue<Vector2>();
            }

            if (lookInput.sqrMagnitude > 0.001f)
            {
                _orbitYaw += lookInput.x * LookSensitivity.x;
                _orbitPitch -= lookInput.y * LookSensitivity.y;
                _orbitPitch = Mathf.Clamp(_orbitPitch, PitchLimits.x, PitchLimits.y);
                _lastLookInputTime = Time.time;
            }
            else
            {
                // Auto-recenter when driving forward
                if (Time.time - _lastLookInputTime > RecenterDelay && _currentSpeedKmh > MinRecenterSpeedKmh)
                {
                    _orbitYaw = Mathf.MoveTowardsAngle(_orbitYaw, 0f, RecenterSpeed * 45f * dt);
                    _orbitPitch = Mathf.MoveTowards(_orbitPitch, 0f, RecenterSpeed * 20f * dt);
                }
            }

            // 4. Update Dynamic FOV on the active camera
            var activeSlot = CurrentSlot;
            if (activeSlot != null && activeSlot.Camera != null && activeSlot.EnableDynamicFOV)
            {
                float speedRatio = Mathf.Clamp01(_currentSpeedKmh / Mathf.Max(1f, MaxFovSpeedKmh));
                float targetFov = Mathf.Lerp(activeSlot.BaseFOV, activeSlot.MaxFOV, speedRatio);
                float currentFov = activeSlot.Camera.Lens.FieldOfView;
                activeSlot.Camera.Lens.FieldOfView = Mathf.MoveTowards(currentFov, targetFov, FovLerpSpeed * 10f * dt);
            }

            // 5. Update look back rotation or orbit on active chase camera
            UpdateChaseCameraOrientation(activeSlot, dt);
        }

        private void UpdateChaseCameraOrientation(VehicleCameraSlot slot, float dt)
        {
            if (slot == null || slot.Camera == null) return;

            // Apply LookBack or manual orbit on Chase / Orbit cameras
            if (slot.Mode == VehicleCameraMode.ChaseNear || slot.Mode == VehicleCameraMode.ChaseFar)
            {
                var follow = slot.Camera.GetComponent<CinemachineFollow>();
                if (follow != null)
                {
                    float targetYaw = _isLookingBack ? 180f : _orbitYaw;
                    Vector3 baseOffset = (slot.Mode == VehicleCameraMode.ChaseNear)
                        ? new Vector3(0f, 1.8f, -5.2f)
                        : new Vector3(0f, 2.7f, -7.5f);

                    // Slightly pull back with speed
                    float speedPullBack = Mathf.Clamp01(_currentSpeedKmh / 160f) * 0.8f;
                    Vector3 dynamicOffset = baseOffset + new Vector3(0f, 0f, -speedPullBack);

                    Quaternion orbitRot = Quaternion.Euler(_orbitPitch, targetYaw, 0f);
                    follow.FollowOffset = orbitRot * dynamicOffset;
                }
            }
        }

        /// <summary>
        /// Ensures default camera slots and references are populated.
        /// </summary>
        public void EnsureCameraSlots()
        {
            if (_cameraSlots == null)
            {
                _cameraSlots = new List<VehicleCameraSlot>();
            }

            // If empty, look for child CinemachineCamera components
            if (_cameraSlots.Count == 0)
            {
                var cams = GetComponentsInChildren<CinemachineCamera>(true);
                for (int i = 0; i < cams.Length; i++)
                {
                    var c = cams[i];
                    string lowerName = c.name.ToLowerInvariant();
                    VehicleCameraMode mode = VehicleCameraMode.ChaseNear;
                    string displayName = "Chase Near";
                    float baseFov = 60f;
                    float maxFov = 74f;

                    if (lowerName.Contains("far"))
                    {
                        mode = VehicleCameraMode.ChaseFar;
                        displayName = "Chase Far";
                        baseFov = 65f;
                        maxFov = 78f;
                    }
                    else if (lowerName.Contains("cockpit") || lowerName.Contains("interior"))
                    {
                        mode = VehicleCameraMode.Cockpit;
                        displayName = "Cockpit";
                        baseFov = 75f;
                        maxFov = 80f;
                    }
                    else if (lowerName.Contains("bumper") || lowerName.Contains("hood"))
                    {
                        mode = VehicleCameraMode.Bumper;
                        displayName = "Bumper / Hood";
                        baseFov = 75f;
                        maxFov = 85f;
                    }
                    else if (lowerName.Contains("orbit") || lowerName.Contains("free"))
                    {
                        mode = VehicleCameraMode.FreeOrbit;
                        displayName = "Free Orbit";
                        baseFov = 60f;
                        maxFov = 60f;
                    }

                    _cameraSlots.Add(new VehicleCameraSlot
                    {
                        Mode = mode,
                        DisplayName = displayName,
                        Camera = c,
                        BaseFOV = baseFov,
                        MaxFOV = maxFov,
                        EnableDynamicFOV = (mode != VehicleCameraMode.FreeOrbit)
                    });
                }
            }
        }
    }
}
