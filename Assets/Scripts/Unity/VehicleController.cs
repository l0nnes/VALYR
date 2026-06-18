using System;
using System.Collections.Generic;
using Core;
using Core.Contexts;
using Core.Math;
using Unity.Data;
using Unity.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity
{
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleController : MonoBehaviour
    {
        [Header("Configuration")] [SerializeField]
        private VehiclePreset vehicleConfigAsset;

        [Header("Scene Setup")] [SerializeField]
        private List<AxleNodes> axleTransforms;

        private float _brake;
        private float _clutch;
        private GameInput _gameInput;

        private UnityVehicleHost _host;
        private bool _isDownshift;
        private bool _isHandbrake;
        private bool _isUpshift;
        private Rigidbody _rb;
        private float _steering;

        private float _throttle;
        private float[] _visualSuspensionLength;
        private float[] _visualWheelAngularVelocity;
        private float[] _visualWheelRotation;

        public VehicleCore Core { get; private set; }

        private void Awake()
        {
            _gameInput = new GameInput();
            _rb = GetComponent<Rigidbody>();

            if (!vehicleConfigAsset)
            {
                Debug.LogError("Vehicle Config Asset is missing!");
                enabled = false;
                return;
            }

            var coreConfig = vehicleConfigAsset.CreateConfig();

            if (coreConfig.axles.Length != axleTransforms.Count)
            {
                Debug.LogError(
                    $"Config defines {coreConfig.axles.Length} axles, but Scene Setup has {axleTransforms.Count}!");
                enabled = false;
                return;
            }

            _host = new UnityVehicleHost(_rb);
            Core = new VehicleCore(coreConfig);

            var hardPoints = new List<Vec3>();

            foreach (var node in axleTransforms)
            {
                var lPos = node.leftHardPoint ? node.leftHardPoint.localPosition : Vector3.zero;
                var rPos = node.rightHardPoint ? node.rightHardPoint.localPosition : Vector3.zero;

                hardPoints.Add(lPos);
                hardPoints.Add(rPos);
            }

            Core.Initialize(_host, hardPoints.ToArray());
            _visualWheelRotation = new float[hardPoints.Count];
            _visualWheelAngularVelocity = new float[hardPoints.Count];
            _visualSuspensionLength = new float[hardPoints.Count];
            foreach (var wheel in Core.Context.wheels)
                if (wheel.index < _visualSuspensionLength.Length)
                    _visualSuspensionLength[wheel.index] = wheel.state.suspension.currentLength;

            _rb.mass = coreConfig.mass + GetUnsprungWheelMass(coreConfig);
            _rb.centerOfMass = coreConfig.centerOfMass;
            _rb.inertiaTensor = Vector3.Scale(_rb.inertiaTensor, coreConfig.inertiaTensorScale);
        }

        private void Update()
        {
            if (Core == null) return;

            _host.SetInput(_throttle, _steering, _brake, _isHandbrake, _isUpshift, _isDownshift, _clutch);

            (_isUpshift, _isDownshift) = (false, false);

            SyncVisuals();
        }

        private void FixedUpdate()
        {
            Core?.Simulate();
        }

        private void OnEnable()
        {
            _gameInput.Enable();
            _gameInput.Driving.ThrottleBrake.performed += OnThrottleBrake;
            _gameInput.Driving.ThrottleBrake.canceled += OnThrottleBrake;
            _gameInput.Driving.Steering.performed += OnSteering;
            _gameInput.Driving.Steering.canceled += OnSteering;
            _gameInput.Driving.Clutch.performed += OnClutch;
            _gameInput.Driving.Clutch.canceled += OnClutch;
            _gameInput.Driving.Handbrake.performed += OnHandbrake;
            _gameInput.Driving.Handbrake.canceled += OnHandbrake;
            _gameInput.Driving.GearboxUp.performed += OnGearboxUp;
            _gameInput.Driving.GearboxDown.performed += OnGearboxDown;
        }

        private void OnDisable()
        {
            _gameInput.Disable();
            _gameInput.Driving.ThrottleBrake.performed -= OnThrottleBrake;
            _gameInput.Driving.ThrottleBrake.canceled -= OnThrottleBrake;
            _gameInput.Driving.Steering.performed -= OnSteering;
            _gameInput.Driving.Steering.canceled -= OnSteering;
            _gameInput.Driving.Clutch.performed -= OnClutch;
            _gameInput.Driving.Clutch.canceled -= OnClutch;
            _gameInput.Driving.Handbrake.performed -= OnHandbrake;
            _gameInput.Driving.Handbrake.canceled -= OnHandbrake;
            _gameInput.Driving.GearboxUp.performed -= OnGearboxUp;
            _gameInput.Driving.GearboxDown.performed -= OnGearboxDown;
        }

        private void OnGearboxDown(InputAction.CallbackContext obj)
        {
            _isDownshift = obj.ReadValueAsButton();
        }

        private void OnGearboxUp(InputAction.CallbackContext obj)
        {
            _isUpshift = obj.ReadValueAsButton();
        }

        private void OnHandbrake(InputAction.CallbackContext obj)
        {
            _isHandbrake = obj.ReadValueAsButton();
        }

        private void OnClutch(InputAction.CallbackContext obj)
        {
            _clutch = obj.ReadValue<float>();
        }

        private void OnSteering(InputAction.CallbackContext obj)
        {
            _steering = obj.ReadValue<float>();
        }

        private void OnThrottleBrake(InputAction.CallbackContext obj)
        {
            var value = obj.ReadValue<float>();
            _throttle = Mathf.Clamp(value, 0f, 1f);
            _brake = -Mathf.Clamp(value, -1f, 0f);
        }

        private static float GetUnsprungWheelMass(VehicleConfig config)
        {
            var mass = 0f;
            if (config.axles == null) return mass;

            foreach (var axle in config.axles)
            {
                if (axle?.wheel == null) continue;
                mass += axle.wheel.mass * 2f;
            }

            return mass;
        }

        private void SyncVisuals()
        {
            if (Core == null) return;

            var axles = Core.Context.axles;

            for (var i = 0; i < axles.Count; i++)
            {
                if (i >= axleTransforms.Count) break;

                var axleCtx = axles[i];
                var node = axleTransforms[i];

                SyncWheelVisual(node.leftVisual, node.leftHardPoint, axleCtx.leftWheel,
                    ref _visualWheelRotation[axleCtx.leftWheel.index]);
                SyncWheelVisual(node.rightVisual, node.rightHardPoint, axleCtx.rightWheel,
                    ref _visualWheelRotation[axleCtx.rightWheel.index]);
            }
        }

        private void SyncWheelVisual(Transform visual, Transform hardPoint, WheelContext wheel, ref float rotationValue)
        {
            if (!visual || !hardPoint) return;

            var targetPos = hardPoint.localPosition;
            var visualLength = _visualSuspensionLength[wheel.index];
            visualLength = Mathf.Lerp(visualLength, wheel.state.suspension.currentLength,
                1f - Mathf.Exp(-18f * Time.deltaTime));
            _visualSuspensionLength[wheel.index] = visualLength;

            targetPos.y -= visualLength;
            visual.localPosition = targetPos;

            var targetOmega = wheel.state.tire.visualAngularVelocity;
            var visualOmega = _visualWheelAngularVelocity[wheel.index];
            var omegaBlend = 1f - Mathf.Exp(-24f * Time.deltaTime);
            visualOmega = Mathf.Lerp(visualOmega, targetOmega, omegaBlend);
            if (Mathf.Abs(targetOmega) < 0.01f && Mathf.Abs(visualOmega) < 0.01f)
                visualOmega = 0f;
            _visualWheelAngularVelocity[wheel.index] = visualOmega;

            rotationValue += visualOmega * Mathf.Rad2Deg * Time.deltaTime;
            rotationValue = Mathf.Repeat(rotationValue, 360f);

            var spinRot = Quaternion.Euler(rotationValue, 0f, 0f);
            var steerRot = Quaternion.Euler(0f, wheel.state.steeringAngle, 0f);

            visual.localRotation = steerRot * spinRot;
        }

        [Serializable]
        public struct AxleNodes
        {
            public string name;
            [Header("Left Side")] public Transform leftHardPoint;
            public Transform leftVisual;

            [Header("Right Side")] public Transform rightHardPoint;
            public Transform rightVisual;
        }
    }
}