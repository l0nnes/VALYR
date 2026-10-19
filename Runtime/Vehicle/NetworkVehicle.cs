using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Unity.Cinemachine;
using PurrNet;
using PurrNet.Prediction;
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
    /// Strictly responsible for:
    /// - PurrNet identity and client-owner authority management
    /// - Cinemachine camera rig binding (all 5 camera slots with C key switching)
    /// - Synchronization adapter for vehicle sub-addons (doors, lights, interior controls)
    /// - Coordinating with PredictedVehicle for client-side prediction and collision physics
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class NetworkVehicle : NetworkBehaviour
    {
        // Cached vehicle components
        private VehicleController _controller;
        private VehicleInputReader _inputReader;
        private VehicleCameraController _cameraController;
        private VehicleTelemetryHUD _telemetryHUD;
        private VehicleVisualView _visualView;
        private PredictedVehicle _predictedVehicle;
        private Rigidbody _rigidbody;
        private PredictedTransform _predictedTransform;

        // Modular addon components
        private NetworkVehicleDoors _netDoors;
        private NetworkVehicleLights _netLights;
        private NetworkVehicleInterior _netInterior;

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

        /// <summary>
        /// Live state provider used by sub-addons (lights, interior dashboard, doors).
        /// Reads directly from PredictedVehicle's current predicted/reconciled simulation state.
        /// </summary>
        public VehicleNetworkState SyncedState
        {
            get
            {
                VehicleState s = _predictedVehicle != null ? _predictedVehicle.currentState.CoreState : (_controller != null ? _controller.State : default);
                VehicleInput vi = _controller != null ? _controller.Input : default;
                if (_predictedVehicle != null && vi.Throttle == 0f && vi.Brake == 0f && vi.Handbrake == 0f)
                {
                    vi = new VehicleInput(_predictedVehicle.currentState.Throttle, _predictedVehicle.currentState.Steer, _predictedVehicle.currentState.Brake, _predictedVehicle.currentState.Handbrake);
                }
                Vector3 vel = _rigidbody != null ? _rigidbody.linearVelocity : Vector3.zero;
                float speedKmh = Vector3.Dot(vel, transform.forward) * 3.6f;

                return new VehicleNetworkState
                {
                    EngineRpm = s.EngineRpm,
                    CurrentGear = s.CurrentGear,
                    SteerAngle = s.Wheels[0].SteerAngle,
                    ForwardSpeed = speedKmh,
                    SuspLength0 = s.Wheels[0].SuspensionLength,
                    SuspLength1 = s.Wheels[1].SuspensionLength,
                    SuspLength2 = s.Wheels[2].SuspensionLength,
                    SuspLength3 = s.Wheels[3].SuspensionLength,
                    WheelOmega0 = s.Wheels[0].AngularVelocity,
                    WheelOmega1 = s.Wheels[1].AngularVelocity,
                    WheelOmega2 = s.Wheels[2].AngularVelocity,
                    WheelOmega3 = s.Wheels[3].AngularVelocity,
                    IsBraking = vi.Brake > 0.05f || (s.CurrentGear == -1 && vi.Throttle > 0.05f),
                    IsHandbraking = vi.Handbrake > 0.5f,
                    IsReversing = s.CurrentGear < 0
                };
            }
        }

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
            if (_predictedVehicle == null) _predictedVehicle = GetComponent<PredictedVehicle>();
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
            if (_predictedTransform == null) _predictedTransform = GetComponent<PredictedTransform>();

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

            // Sync ownership with PredictedVehicle
            if (_predictedVehicle != null)
            {
                PlayerID? targetOwner = AssignedPlayerId.HasValue ? AssignedPlayerId : (isSpawned ? owner : null);
                if (targetOwner.HasValue && _predictedVehicle.owner != targetOwner)
                {
                    _predictedVehicle.owner = targetOwner;
                }
            }

            // Input reader
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

            // Camera controller & Cinemachine bindings
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

            // UI HUD
            if (_telemetryHUD != null)
                _telemetryHUD.enabled = localControl;

            // Sub-addon components
            if (_netDoors != null) _netDoors.UpdateAuthorityState();
            if (_netLights != null) _netLights.IsolateLightControllerInput();
            if (_netInterior != null) _netInterior.UpdateAuthorityState();
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target?.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(target, value);
        }

        private void Update()
        {
            // Direct camera switching fallback for local driver (C key)
            if (IsLocallyControlled)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null)
                {
                    if (kb.cKey.wasPressedThisFrame && _cameraController != null && _cameraController.enabled)
                    {
                        _cameraController.CycleNextCamera();
                    }
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

            Transform visualRoot = (_predictedTransform != null && _predictedTransform.graphics != null)
                ? _predictedTransform.graphics
                : (transform.Find("Graphics") ?? transform.Find("Meshes") ?? transform);

            Transform lookTarget = transform.Find("Graphics/CameraAnchors/LookTarget")
                ?? transform.Find("CameraAnchors/LookTarget")
                ?? transform.Find("LookTarget")
                ?? visualRoot;

            Transform cockpit = transform.Find("Graphics/CameraAnchors/CockpitAnchor")
                ?? transform.Find("CameraAnchors/CockpitAnchor")
                ?? transform.Find("CockpitAnchor")
                ?? visualRoot;

            Transform bumper = transform.Find("Graphics/CameraAnchors/BumperAnchor")
                ?? transform.Find("CameraAnchors/BumperAnchor")
                ?? transform.Find("BumperAnchor")
                ?? visualRoot;

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
                    vcam.Target.TrackingTarget = cockpit;
                    vcam.Target.LookAtTarget = null;
                }
                else if (vcamName.Contains("bumper") || vcamName.Contains("hood"))
                {
                    bumperCam = vcam;
                    vcam.Target.TrackingTarget = bumper;
                    vcam.Target.LookAtTarget = null;
                }
                else if (vcamName.Contains("far"))
                {
                    chaseFar = vcam;
                    vcam.Target.TrackingTarget = visualRoot;
                    vcam.Target.LookAtTarget = lookTarget;
                    ConfigureChaseDamping(vcam);
                }
                else if (vcamName.Contains("orbit") || vcamName.Contains("free"))
                {
                    freeLook = vcam;
                    vcam.Target.TrackingTarget = visualRoot;
                    vcam.Target.LookAtTarget = lookTarget;
                }
                else if (vcamName.Contains("near") || vcamName.Contains("chase"))
                {
                    chaseNear = vcam;
                    vcam.Target.TrackingTarget = visualRoot;
                    vcam.Target.LookAtTarget = lookTarget;
                    ConfigureChaseDamping(vcam);
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
