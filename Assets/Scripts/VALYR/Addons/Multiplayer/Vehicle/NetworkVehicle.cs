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
    /// Base multiplayer adapter for VALYR vehicles over PurrNet.
    /// Strictly responsible for core functionality:
    /// - Dynamic PhysX Rigidbody configuration and collision response without belly sagging
    /// - Client-owner authority management and input isolation
    /// - Cinemachine camera rig binding (all 5 camera slots with C key switching)
    /// - Silky-smooth wheel visuals (independent per-wheel angular velocity roll integration and smooth steer angle lerp)
    /// 
    /// Visual addons (doors, lights, interior controls & dashboard) are managed by dedicated
    /// modular components: NetworkVehicleDoors, NetworkVehicleLights, NetworkVehicleInterior.
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
        private NetworkRigidbody _networkRigidbody;
        private Rigidbody _rigidbody;

        // Modular addon components
        private NetworkVehicleDoors _netDoors;
        private NetworkVehicleLights _netLights;
        private NetworkVehicleInterior _netInterior;

        // Smooth wheel visual interpolation for remote proxies
        private float _smoothedSteerAngle;
        private float _proxyWheelRoll0;
        private float _proxyWheelRoll1;
        private float _proxyWheelRoll2;
        private float _proxyWheelRoll3;

        private static readonly FieldInfo CameraSlotsField =
            typeof(VehicleCameraController).GetField("_cameraSlots", BindingFlags.NonPublic | BindingFlags.Instance);

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
            EnsureModularComponentsAttached();
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

            if (_netDoors == null) _netDoors = GetComponent<NetworkVehicleDoors>();
            if (_netLights == null) _netLights = GetComponent<NetworkVehicleLights>();
            if (_netInterior == null) _netInterior = GetComponent<NetworkVehicleInterior>();
        }

        private void EnsureModularComponentsAttached()
        {
            if (_netDoors == null)
            {
                _netDoors = GetComponent<NetworkVehicleDoors>();
                if (_netDoors == null) _netDoors = gameObject.AddComponent<NetworkVehicleDoors>();
            }

            if (_netLights == null)
            {
                _netLights = GetComponent<NetworkVehicleLights>();
                if (_netLights == null) _netLights = gameObject.AddComponent<NetworkVehicleLights>();
            }

            if (_netInterior == null)
            {
                _netInterior = GetComponent<NetworkVehicleInterior>();
                if (_netInterior == null) _netInterior = gameObject.AddComponent<NetworkVehicleInterior>();
            }
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
                    // Prevent VehicleInputReader.OnDisable() from disabling the shared InputActionMap for the local player
                    SetPrivateField(_inputReader, "_actionMap", null);
                }
                _inputReader.enabled = localControl;
                if (localControl)
                {
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

            // 4. Rigidbody physics configuration
            if (_rigidbody != null)
            {
                // Dynamic PhysX ensures realistic two-car collisions and momentum transfer
                _rigidbody.isKinematic = false;
                _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

                if (localControl)
                {
                    // Local driver: full gravity supported by local suspension raycasts
                    _rigidbody.useGravity = true;
                }
                else
                {
                    // Remote proxy: zero gravity to prevent sagging while keeping full dynamic collision responses
                    _rigidbody.useGravity = false;
                }
            }

            // 5. Notify modular addon components of authority changes
            if (_netDoors != null) _netDoors.UpdateAuthorityState();
            if (_netLights != null) _netLights.IsolateLightControllerInput();
            if (_netInterior != null) _netInterior.UpdateAuthorityState();
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target?.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(target, value);
        }

        private void FixedUpdate()
        {
            // Owner writes local physics simulation snapshot to SyncVar
            if (isSpawned && isOwner && _controller != null)
            {
                VehicleState state = _controller.State;
                VehicleInput input = _controller.Input;

                Vector3 vel = _rigidbody != null ? _rigidbody.linearVelocity : Vector3.zero;
                float forwardSpeed = Vector3.Dot(vel, transform.forward);

                var net = new VehicleNetworkState
                {
                    EngineRpm = state.EngineRpm,
                    CurrentGear = state.CurrentGear,
                    SteerAngle = state.Wheels[0].SteerAngle,
                    ForwardSpeed = forwardSpeed,
                    SuspLength0 = state.Wheels[0].SuspensionLength,
                    SuspLength1 = state.Wheels[1].SuspensionLength,
                    SuspLength2 = state.Wheels[2].SuspensionLength,
                    SuspLength3 = state.Wheels[3].SuspensionLength,
                    WheelOmega0 = state.Wheels[0].AngularVelocity,
                    WheelOmega1 = state.Wheels[1].AngularVelocity,
                    WheelOmega2 = state.Wheels[2].AngularVelocity,
                    WheelOmega3 = state.Wheels[3].AngularVelocity,
                    IsBraking = input.Brake > 0.05f || (state.CurrentGear == -1 && input.Throttle > 0.05f),
                    IsHandbraking = input.Handbrake > 0.5f,
                    IsReversing = state.CurrentGear < 0
                };

                _networkState.value = net;
            }
        }

        private void Update()
        {
            // 1. Direct keyboard fallback for local driver
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
                }
            }

            // 2. Remote proxy: render silky-smooth wheel visuals
            if (isSpawned && !isOwner)
            {
                float dt = Time.deltaTime;
                VehicleNetworkState net = _networkState.value;

                // Smooth steer angle exponential interpolation
                _smoothedSteerAngle = Mathf.Lerp(_smoothedSteerAngle, net.SteerAngle, 1f - Mathf.Exp(-22f * dt));

                // Continuous per-wheel roll integration using exact physical angular velocity
                // Accurately locks rear wheels on handbrake, locks wheels under heavy braking, and spins on burnouts
                _proxyWheelRoll0 += net.WheelOmega0 * dt;
                _proxyWheelRoll1 += net.WheelOmega1 * dt;
                _proxyWheelRoll2 += net.WheelOmega2 * dt;
                _proxyWheelRoll3 += net.WheelOmega3 * dt;

                _proxyWheelRoll0 = Mathf.Repeat(_proxyWheelRoll0, Mathf.PI * 2f);
                _proxyWheelRoll1 = Mathf.Repeat(_proxyWheelRoll1, Mathf.PI * 2f);
                _proxyWheelRoll2 = Mathf.Repeat(_proxyWheelRoll2, Mathf.PI * 2f);
                _proxyWheelRoll3 = Mathf.Repeat(_proxyWheelRoll3, Mathf.PI * 2f);

                if (_visualView != null)
                {
                    VehicleState proxyState = default;
                    proxyState.EngineRpm = net.EngineRpm;
                    proxyState.CurrentGear = net.CurrentGear;

                    proxyState.Wheels.FL.SteerAngle = _smoothedSteerAngle;
                    proxyState.Wheels.FR.SteerAngle = _smoothedSteerAngle;
                    proxyState.Wheels.RL.SteerAngle = 0f;
                    proxyState.Wheels.RR.SteerAngle = 0f;

                    // Fallback to nominal suspension length (0.38f) if not yet received
                    proxyState.Wheels.FL.SuspensionLength = net.SuspLength0 > 0.001f ? net.SuspLength0 : 0.38f;
                    proxyState.Wheels.FR.SuspensionLength = net.SuspLength1 > 0.001f ? net.SuspLength1 : 0.38f;
                    proxyState.Wheels.RL.SuspensionLength = net.SuspLength2 > 0.001f ? net.SuspLength2 : 0.38f;
                    proxyState.Wheels.RR.SuspensionLength = net.SuspLength3 > 0.001f ? net.SuspLength3 : 0.38f;

                    proxyState.Wheels.FL.RotationAngle = _proxyWheelRoll0;
                    proxyState.Wheels.FR.RotationAngle = _proxyWheelRoll1;
                    proxyState.Wheels.RL.RotationAngle = _proxyWheelRoll2;
                    proxyState.Wheels.RR.RotationAngle = _proxyWheelRoll3;

                    _visualView.UpdateWheelsView(in proxyState, dt);
                }
            }
        }

        /// <summary>
        /// Binds scene Cinemachine cameras to follow this vehicle for the local player,
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
