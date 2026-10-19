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
    /// dynamic speed FOV expansion, smooth damped look-around with auto-recentering, and quick Look Back.
    /// </summary>
    [DefaultExecutionOrder(2000)]
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

        [Header("Chase Orbit & Look Tuning")]
        [Tooltip("Mouse look sensitivity (degrees per pixel).")]
        [Range(0.01f, 1f)]
        public float MouseSensitivity = 0.12f;

        [Tooltip("Gamepad stick look sensitivity (degrees per second).")]
        [Range(30f, 360f)]
        public float GamepadSensitivity = 120f;

        [Tooltip("Smoothing time (seconds) for camera orbit movement. Higher = smoother/heavier, lower = snappier.")]
        [Range(0.02f, 0.3f)]
        public float OrbitDamping = 0.08f;

        [Tooltip("Smoothing time (seconds) when transitioning into/out of Look Back (180 deg).")]
        [Range(0.02f, 0.25f)]
        public float LookBackDamping = 0.06f;

        [Tooltip("Vertical pitch clamp limits in degrees (min = looking up, max = looking down).")]
        public Vector2 PitchLimits = new Vector2(-10f, 40f);

        [Tooltip("Seconds of no look input before the chase camera auto-recenters behind the moving car.")]
        public float RecenterDelay = 1.0f;

        [Tooltip("Speed (degrees/second) at which target angle returns to center.")]
        public float RecenterSpeed = 90.0f;

        [Tooltip("Minimum vehicle speed (km/h) required for auto-recentering to activate.")]
        public float MinRecenterSpeedKmh = 4.0f;

        [Tooltip("Optional: Require holding Right Mouse Button to rotate camera with mouse.")]
        public bool RequireRightClickForMouseLook = false;

        [Header("Cockpit Head Look")]
        [Tooltip("Enable free head rotation look-around in Cockpit first-person view.")]
        public bool EnableCockpitHeadLook = true;

        [Tooltip("Horizontal yaw rotation limits in degrees (negative = look left, positive = look right).")]
        public Vector2 CockpitYawLimits = new Vector2(-105f, 105f);

        [Tooltip("Vertical pitch rotation limits in degrees (negative = look up, positive = look down).")]
        public Vector2 CockpitPitchLimits = new Vector2(-28f, 35f);

        [Tooltip("Cockpit look-back yaw angle (degrees) to glance through the rear window when Look Back is held.")]
        [Range(120f, 180f)]
        public float CockpitLookBackYaw = 165f;

        [Tooltip("Cockpit mouse look sensitivity (degrees per pixel).")]
        [Range(0.01f, 1f)]
        public float CockpitMouseSensitivity = 0.12f;

        [Tooltip("Cockpit gamepad look sensitivity (degrees per second).")]
        [Range(30f, 360f)]
        public float CockpitGamepadSensitivity = 110f;

        [Tooltip("Cockpit head rotation damping time (seconds).")]
        [Range(0.02f, 0.2f)]
        public float CockpitDamping = 0.06f;

        [Tooltip("Enable auto-recentering head look forward in cockpit.")]
        public bool EnableCockpitRecenter = true;

        [Tooltip("Seconds of no look input before cockpit camera auto-recenters forward.")]
        public float CockpitRecenterDelay = 1.2f;

        [Tooltip("Speed (degrees/second) at which head returns to center forward.")]
        public float CockpitRecenterSpeed = 110f;

        [Tooltip("Require holding Right Mouse Button to rotate head in cockpit.")]
        public bool RequireRightClickInCockpit = false;

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

        [Header("Cinemachine Manual Update")]
        [Tooltip("If true, drives CinemachineBrain.ManualUpdate() directly in LateUpdate for zero-latency jitter-free tracking.")]
        public bool DriveManualUpdate = true;

        private CinemachineBrain _cinemachineBrain;

        // Internal orbit target and smoothed state
        private float _targetYaw;
        private float _targetPitch;
        private float _currentYaw;
        private float _currentPitch;
        private float _yawVelocity;
        private float _pitchVelocity;
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

            if (_cockpitAnchor == null)
            {
                _cockpitAnchor = transform.Find("Graphics/CameraAnchors/CockpitAnchor")
                    ?? transform.Find("CameraAnchors/CockpitAnchor")
                    ?? transform.Find("CockpitAnchor");
            }

            if (_bumperAnchor == null)
            {
                _bumperAnchor = transform.Find("Graphics/CameraAnchors/BumperAnchor")
                    ?? transform.Find("CameraAnchors/BumperAnchor")
                    ?? transform.Find("BumperAnchor");
            }

            if (_lookTarget == null)
            {
                _lookTarget = transform.Find("Graphics/CameraAnchors/LookTarget")
                    ?? transform.Find("CameraAnchors/LookTarget")
                    ?? transform.Find("LookTarget");
            }

            InitializeInputActions();
            EnsureCinemachineBrain();
        }

        private void EnsureCinemachineBrain()
        {
            if (_cinemachineBrain == null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    _cinemachineBrain = cam.GetComponent<CinemachineBrain>();
                }
                if (_cinemachineBrain == null)
                {
                    _cinemachineBrain = FindAnyObjectByType<CinemachineBrain>();
                }
            }

            if (_cinemachineBrain != null && DriveManualUpdate)
            {
                _cinemachineBrain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            }
        }

        private void Start()
        {
            EnsureCinemachineBrain();
            EnsureCameraSlots();
            ApplyCameraPriorities();
        }

        private void OnEnable()
        {
            EnsureCinemachineBrain();
            EnsureCameraSlots();
            ApplyCameraPriorities();

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

            if (_cinemachineBrain != null && DriveManualUpdate)
            {
                _cinemachineBrain.UpdateMethod = CinemachineBrain.UpdateMethods.SmartUpdate;
            }
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

            ResetLookAngles();
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
                    ResetLookAngles();
                    _currentSlotIndex = i;
                    ApplyCameraPriorities();
                    OnCameraChanged?.Invoke(CurrentSlot);
                    return;
                }
            }
        }

        private void ResetLookAngles()
        {
            _targetYaw = 0f;
            _targetPitch = 0f;
            _currentYaw = 0f;
            _currentPitch = 0f;
            _yawVelocity = 0f;
            _pitchVelocity = 0f;
            if (_cockpitAnchor != null)
            {
                _cockpitAnchor.localRotation = Quaternion.identity;
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

            // 1. Read Look Back input
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

            var activeSlot = CurrentSlot;
            bool isCockpit = (activeSlot != null && activeSlot.Mode == VehicleCameraMode.Cockpit);

            // 2. Read and process manual look/orbit input
            Vector2 rawLook = Vector2.zero;
            if (_lookAction != null)
            {
                rawLook = _lookAction.ReadValue<Vector2>();
            }

            bool hasLookInput = false;
            if (rawLook.sqrMagnitude > 0.0001f)
            {
                bool isGamepad = _lookAction != null && _lookAction.activeControl?.device is Gamepad;
                float deltaYaw;
                float deltaPitch;

                if (isGamepad)
                {
                    float sens = (isCockpit && EnableCockpitHeadLook) ? CockpitGamepadSensitivity : GamepadSensitivity;
                    deltaYaw = rawLook.x * sens * dt;
                    deltaPitch = rawLook.y * sens * dt;
                    hasLookInput = true;
                }
                else
                {
                    bool requireClick = (isCockpit && EnableCockpitHeadLook) ? RequireRightClickInCockpit : RequireRightClickForMouseLook;
                    if (!requireClick || (Mouse.current != null && Mouse.current.rightButton.isPressed))
                    {
                        float sens = (isCockpit && EnableCockpitHeadLook) ? CockpitMouseSensitivity : MouseSensitivity;
                        deltaYaw = rawLook.x * sens;
                        deltaPitch = rawLook.y * sens;
                        hasLookInput = true;
                    }
                    else
                    {
                        deltaYaw = 0f;
                        deltaPitch = 0f;
                    }
                }

                if (hasLookInput)
                {
                    _targetYaw += deltaYaw;
                    _targetPitch -= deltaPitch;

                    if (isCockpit && EnableCockpitHeadLook)
                    {
                        _targetPitch = Mathf.Clamp(_targetPitch, CockpitPitchLimits.x, CockpitPitchLimits.y);
                        _targetYaw = Mathf.Clamp(_targetYaw, CockpitYawLimits.x, CockpitYawLimits.y);
                    }
                    else
                    {
                        _targetPitch = Mathf.Clamp(_targetPitch, PitchLimits.x, PitchLimits.y);
                    }

                    _lastLookInputTime = Time.time;
                }
            }

            // 3. Auto-recenter when not actively looking
            if (!hasLookInput && !_isLookingBack)
            {
                if (isCockpit && EnableCockpitHeadLook)
                {
                    if (EnableCockpitRecenter && Time.time - _lastLookInputTime > CockpitRecenterDelay)
                    {
                        _targetYaw = Mathf.MoveTowardsAngle(_targetYaw, 0f, CockpitRecenterSpeed * dt);
                        _targetPitch = Mathf.MoveTowards(_targetPitch, 0f, CockpitRecenterSpeed * 0.5f * dt);
                    }
                }
                else
                {
                    if (Time.time - _lastLookInputTime > RecenterDelay && _currentSpeedKmh > MinRecenterSpeedKmh)
                    {
                        _targetYaw = Mathf.MoveTowardsAngle(_targetYaw, 0f, RecenterSpeed * dt);
                        _targetPitch = Mathf.MoveTowards(_targetPitch, 0f, RecenterSpeed * 0.5f * dt);
                    }
                }
            }
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;

            // 1. Calculate and smoothly filter vehicle speed to eliminate Z-axis pullback jitter
            if (_vehicle != null && _vehicle.Rigidbody != null)
            {
                Vector3 vel = _vehicle.Rigidbody.linearVelocity;
                float forwardSpeed = Vector3.Dot(vel, _vehicle.transform.forward);
                float rawSpeedKmh = Mathf.Abs(forwardSpeed) * 3.6f;
                _currentSpeedKmh = Mathf.MoveTowards(_currentSpeedKmh, rawSpeedKmh, 80f * dt);
            }

            var activeSlot = CurrentSlot;
            bool isCockpit = (activeSlot != null && activeSlot.Mode == VehicleCameraMode.Cockpit);

            // 2. Smoothly damp current yaw & pitch towards target
            float desiredYaw;
            float desiredPitch;
            float currentDampTime;

            if (isCockpit && EnableCockpitHeadLook)
            {
                desiredYaw = _isLookingBack ? CockpitLookBackYaw : _targetYaw;
                desiredPitch = _isLookingBack ? 0f : _targetPitch;
                currentDampTime = _isLookingBack ? LookBackDamping : CockpitDamping;
            }
            else
            {
                desiredYaw = _isLookingBack ? 180f : _targetYaw;
                desiredPitch = _isLookingBack ? 0f : _targetPitch;
                currentDampTime = _isLookingBack ? LookBackDamping : OrbitDamping;
            }

            _currentYaw = Mathf.SmoothDampAngle(_currentYaw, desiredYaw, ref _yawVelocity, currentDampTime);
            _currentPitch = Mathf.SmoothDamp(_currentPitch, desiredPitch, ref _pitchVelocity, currentDampTime);

            // 3. Update Dynamic FOV on the active camera
            if (activeSlot != null && activeSlot.Camera != null && activeSlot.EnableDynamicFOV)
            {
                float speedRatio = Mathf.Clamp01(_currentSpeedKmh / Mathf.Max(1f, MaxFovSpeedKmh));
                float targetFov = Mathf.Lerp(activeSlot.BaseFOV, activeSlot.MaxFOV, speedRatio);
                float currentFov = activeSlot.Camera.Lens.FieldOfView;
                activeSlot.Camera.Lens.FieldOfView = Mathf.MoveTowards(currentFov, targetFov, FovLerpSpeed * 10f * dt);
            }

            // 4. Update camera orientations and offsets
            UpdateCameraOrientations(activeSlot, isCockpit, dt);

            // 5. If ManualUpdate mode is active, trigger CinemachineBrain right after vehicle positions are resolved
            if (DriveManualUpdate)
            {
                EnsureCinemachineBrain();
                if (_cinemachineBrain != null)
                {
                    if (_cinemachineBrain.UpdateMethod != CinemachineBrain.UpdateMethods.ManualUpdate)
                    {
                        _cinemachineBrain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
                    }
                    _cinemachineBrain.ManualUpdate();
                }
            }
        }

        private void UpdateCameraOrientations(VehicleCameraSlot slot, bool isCockpit, float dt)
        {
            if (isCockpit && EnableCockpitHeadLook)
            {
                if (_cockpitAnchor != null)
                {
                    _cockpitAnchor.localRotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
                }
                return;
            }

            if (_cockpitAnchor != null && _cockpitAnchor.localRotation != Quaternion.identity)
            {
                _cockpitAnchor.localRotation = Quaternion.identity;
            }

            if (slot == null || slot.Camera == null) return;

            // Apply smooth damped orbit on Chase cameras
            if (slot.Mode == VehicleCameraMode.ChaseNear || slot.Mode == VehicleCameraMode.ChaseFar)
            {
                var follow = slot.Camera.GetComponent<CinemachineFollow>();
                if (follow != null)
                {
                    Vector3 baseOffset = (slot.Mode == VehicleCameraMode.ChaseNear)
                        ? new Vector3(0f, 1.8f, -5.2f)
                        : new Vector3(0f, 2.7f, -7.5f);

                    // Dynamic subtle pull back with speed
                    float speedPullBack = Mathf.Clamp01(_currentSpeedKmh / 160f) * 0.8f;
                    Vector3 dynamicOffset = baseOffset + new Vector3(0f, 0f, -speedPullBack);

                    // Apply smoothly damped orbit rotation
                    Quaternion orbitRot = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
                    follow.FollowOffset = orbitRot * dynamicOffset;
                }
            }
        }

        /// <summary>
        /// <summary>
        /// Ensures default camera slots and references are populated.
        /// </summary>
        public void EnsureCameraSlots()
        {
            if (_cameraSlots == null || _cameraSlots.Count == 0 || _cameraSlots.Exists(s => s == null || s.Camera == null))
            {
                AutoBindSceneCameras();
            }
        }

        /// <summary>
        /// Automatically discovers scene Cinemachine cameras and binds their targets to this vehicle.
        /// Configures tracking anchors, damping, and default priorities.
        /// </summary>
        public void AutoBindSceneCameras(Transform visualRoot = null, Transform lookTarget = null)
        {
            if (visualRoot == null)
            {
                visualRoot = transform.Find("Graphics") ?? transform.Find("Meshes") ?? transform;
            }

            if (_lookTarget == null)
            {
                _lookTarget = lookTarget ?? transform.Find("Graphics/CameraAnchors/LookTarget")
                    ?? transform.Find("CameraAnchors/LookTarget")
                    ?? transform.Find("LookTarget")
                    ?? visualRoot;
            }

            if (_cockpitAnchor == null)
            {
                _cockpitAnchor = transform.Find("Graphics/CameraAnchors/CockpitAnchor")
                    ?? transform.Find("CameraAnchors/CockpitAnchor")
                    ?? transform.Find("CockpitAnchor")
                    ?? visualRoot;
            }

            if (_bumperAnchor == null)
            {
                _bumperAnchor = transform.Find("Graphics/CameraAnchors/BumperAnchor")
                    ?? transform.Find("CameraAnchors/BumperAnchor")
                    ?? transform.Find("BumperAnchor")
                    ?? visualRoot;
            }

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
                    vcam.Target.TrackingTarget = _cockpitAnchor;
                    vcam.Target.LookAtTarget = null;
                }
                else if (vcamName.Contains("bumper") || vcamName.Contains("hood"))
                {
                    bumperCam = vcam;
                    vcam.Target.TrackingTarget = _bumperAnchor;
                    vcam.Target.LookAtTarget = null;
                }
                else if (vcamName.Contains("far"))
                {
                    chaseFar = vcam;
                    vcam.Target.TrackingTarget = visualRoot;
                    vcam.Target.LookAtTarget = _lookTarget;
                    ConfigureChaseDamping(vcam);
                }
                else if (vcamName.Contains("orbit") || vcamName.Contains("free"))
                {
                    freeLook = vcam;
                    vcam.Target.TrackingTarget = visualRoot;
                    vcam.Target.LookAtTarget = _lookTarget;
                }
                else if (vcamName.Contains("near") || vcamName.Contains("chase"))
                {
                    chaseNear = vcam;
                    vcam.Target.TrackingTarget = visualRoot;
                    vcam.Target.LookAtTarget = _lookTarget;
                    ConfigureChaseDamping(vcam);
                }
            }

            if (_cameraSlots == null)
                _cameraSlots = new List<VehicleCameraSlot>();
            else
                _cameraSlots.Clear();

            if (chaseNear != null)
                _cameraSlots.Add(new VehicleCameraSlot { Mode = VehicleCameraMode.ChaseNear, DisplayName = "Chase Near", Camera = chaseNear, BaseFOV = 60f, MaxFOV = 74f, EnableDynamicFOV = true });
            if (chaseFar != null)
                _cameraSlots.Add(new VehicleCameraSlot { Mode = VehicleCameraMode.ChaseFar, DisplayName = "Chase Far", Camera = chaseFar, BaseFOV = 65f, MaxFOV = 78f, EnableDynamicFOV = true });
            if (cockpitCam != null)
                _cameraSlots.Add(new VehicleCameraSlot { Mode = VehicleCameraMode.Cockpit, DisplayName = "Cockpit", Camera = cockpitCam, BaseFOV = 75f, MaxFOV = 80f, EnableDynamicFOV = true });
            if (bumperCam != null)
                _cameraSlots.Add(new VehicleCameraSlot { Mode = VehicleCameraMode.Bumper, DisplayName = "Bumper / Hood", Camera = bumperCam, BaseFOV = 75f, MaxFOV = 85f, EnableDynamicFOV = true });
            if (freeLook != null)
                _cameraSlots.Add(new VehicleCameraSlot { Mode = VehicleCameraMode.FreeOrbit, DisplayName = "Free Orbit", Camera = freeLook, BaseFOV = 60f, MaxFOV = 60f, EnableDynamicFOV = false });

            if (_cameraSlots.Count == 0)
            {
                var cams = GetComponentsInChildren<CinemachineCamera>(true);
                for (int i = 0; i < cams.Length; i++)
                {
                    _cameraSlots.Add(new VehicleCameraSlot
                    {
                        Mode = (VehicleCameraMode)Mathf.Clamp(i, 0, 4),
                        DisplayName = cams[i].name,
                        Camera = cams[i],
                        BaseFOV = 60f,
                        MaxFOV = 75f,
                        EnableDynamicFOV = true
                    });
                }
            }

            EnsureCinemachineBrain();
            SetCameraMode(VehicleCameraMode.ChaseNear);
            ApplyCameraPriorities();
        }

        private static void ConfigureChaseDamping(CinemachineCamera vcam)
        {
            if (vcam == null) return;
            if (vcam.TryGetComponent<CinemachineFollow>(out var follow))
            {
                follow.TrackerSettings.PositionDamping = new Vector3(0.08f, 0.08f, 0.08f);
                follow.TrackerSettings.RotationDamping = new Vector3(0.08f, 0.08f, 0.08f);
            }
            if (vcam.TryGetComponent<CinemachineRotationComposer>(out var rotComp))
            {
                rotComp.Damping = new Vector2(0.08f, 0.08f);
            }
        }
    }
}
