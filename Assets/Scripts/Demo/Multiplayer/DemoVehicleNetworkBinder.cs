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

            _cameraController.AutoBindSceneCameras(visualRoot, lookTarget);
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
