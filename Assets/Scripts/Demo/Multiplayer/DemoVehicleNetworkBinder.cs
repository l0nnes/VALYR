using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Unity.Cinemachine;
using PurrNet.Prediction;
using VALYR.Multiplayer;
using VALYR.Demo;
using VALYR.Runtime.Cameras;
using VALYR.Runtime.UI;

namespace VALYR.Demo.Multiplayer
{
    /// <summary>
    /// Demo-layer adapter that connects NetworkVehicle authority changes to:
    /// - Cinemachine camera rigs and camera switching (C key)
    /// - VehicleTelemetryHUD visibility
    /// - Vehicle cosmetic addons (doors, lights, interior animation)
    /// </summary>
    [RequireComponent(typeof(NetworkVehicle))]
    public class DemoVehicleNetworkBinder : MonoBehaviour
    {
        private NetworkVehicle _netVehicle;
        private VehicleCameraController _cameraController;
        private VehicleTelemetryHUD _telemetryHUD;
        private PredictedTransform _predictedTransform;

        private static readonly FieldInfo CameraSlotsField =
            typeof(VehicleCameraController).GetField("_cameraSlots", BindingFlags.NonPublic | BindingFlags.Instance);

        private void Awake()
        {
            _netVehicle = GetComponent<NetworkVehicle>();
            _cameraController = GetComponent<VehicleCameraController>();
            _telemetryHUD = GetComponent<VehicleTelemetryHUD>();
            _predictedTransform = GetComponent<PredictedTransform>();

            if (_netVehicle != null)
            {
                _netVehicle.OnAuthorityChanged += HandleAuthorityChanged;
            }
        }

        private void OnDestroy()
        {
            if (_netVehicle != null)
            {
                _netVehicle.OnAuthorityChanged -= HandleAuthorityChanged;
            }
        }

        private void Start()
        {
            if (_netVehicle != null)
            {
                HandleAuthorityChanged(_netVehicle.IsLocallyControlled);
            }
        }

        private void HandleAuthorityChanged(bool localControl)
        {
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
            {
                _telemetryHUD.enabled = localControl;
            }
        }

        private void Update()
        {
            if (_netVehicle != null && _netVehicle.IsLocallyControlled)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.cKey.wasPressedThisFrame && _cameraController != null && _cameraController.enabled)
                {
                    _cameraController.CycleNextCamera();
                }
            }
        }

        public void RebindCinemachineCameras()
        {
            if (_cameraController == null) return;

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

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target?.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}

namespace VALYR.Multiplayer
{
    public static class DemoVehicleExtensions
    {
        public static void RebindCinemachineCameras(this NetworkVehicle vehicle)
        {
            if (vehicle == null) return;
            var binder = vehicle.GetComponent<VALYR.Demo.Multiplayer.DemoVehicleNetworkBinder>() ?? vehicle.gameObject.AddComponent<VALYR.Demo.Multiplayer.DemoVehicleNetworkBinder>();
            binder.RebindCinemachineCameras();
        }
    }
}
