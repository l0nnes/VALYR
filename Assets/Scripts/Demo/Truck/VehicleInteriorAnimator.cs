using UnityEngine;
using VALYR.Core;

namespace VALYR.Runtime.Visual
{
    /// <summary>
    /// Animates interior driver controls (steering wheel, accelerator pedal, brake pedal, handbrake lever)
    /// in real-time matching driver inputs and wheel steering kinematics.
    /// </summary>
    public class VehicleInteriorAnimator : MonoBehaviour
    {
        [Header("Vehicle Reference")]
        [SerializeField] private VehicleController _controller;

        [Header("Interior Controls")]
        [Tooltip("Steering wheel transform inside the cabin.")]
        public Transform SteeringWheel;

        [Tooltip("Steering wheel rotation multiplier in degrees (e.g. 450 = 1.25 turns lock-to-lock).")]
        [Range(90f, 720f)]
        public float MaxSteeringRotation = 450f;

        [Tooltip("Accelerator pedal transform.")]
        public Transform AcceleratorPedal;
        public float AcceleratorMaxAngle = 15f;

        [Tooltip("Brake pedal transform.")]
        public Transform BrakePedal;
        public float BrakeMaxAngle = 12f;

        [Tooltip("Handbrake lever transform.")]
        public Transform HandbrakeLever;
        public float HandbrakeMaxAngle = 25f;

        [Header("Smoothing")]
        [Range(5f, 40f)]
        public float SmoothingSpeed = 18f;

        private Quaternion _baseSteeringRot;
        private Quaternion _baseAccelRot;
        private Quaternion _baseBrakeRot;
        private Quaternion _baseHandbrakeRot;

        private void Awake()
        {
            if (_controller == null)
            {
                _controller = GetComponent<VehicleController>() ?? GetComponentInParent<VehicleController>();
            }

            if (SteeringWheel == null)
                SteeringWheel = transform.Find("Graphics/Meshes/Interior/Interior Steering Wheel") ??
                                transform.Find("Meshes/Interior/Interior Steering Wheel");

            if (AcceleratorPedal == null)
                AcceleratorPedal = transform.Find("Graphics/Meshes/Interior/Interior Accelerator") ??
                                   transform.Find("Meshes/Interior/Interior Accelerator");

            if (BrakePedal == null)
                BrakePedal = transform.Find("Graphics/Meshes/Interior/Interior Brake") ??
                             transform.Find("Meshes/Interior/Interior Brake");

            if (HandbrakeLever == null)
                HandbrakeLever = transform.Find("Graphics/Meshes/Interior/Interior Handbrake") ??
                                 transform.Find("Meshes/Interior/Interior Handbrake");

            if (SteeringWheel != null) _baseSteeringRot = SteeringWheel.localRotation;
            if (AcceleratorPedal != null) _baseAccelRot = AcceleratorPedal.localRotation;
            if (BrakePedal != null) _baseBrakeRot = BrakePedal.localRotation;
            if (HandbrakeLever != null) _baseHandbrakeRot = HandbrakeLever.localRotation;
        }

        private void Update()
        {
            if (_controller == null) return;

            float dt = Time.deltaTime;
            VehicleInput input = _controller.Input;
            VehicleState state = _controller.State;

            // 1. Steering wheel rotation (around local Z axis)
            if (SteeringWheel != null)
            {
                float steerNormalized = Mathf.Abs(input.Steer) > 0.001f
                    ? input.Steer
                    : Mathf.Clamp(state.Wheels[0].SteerAngle / 0.61f, -1f, 1f);
                Quaternion targetSteering = _baseSteeringRot * Quaternion.Euler(0f, 0f, -steerNormalized * MaxSteeringRotation);
                SteeringWheel.localRotation = Quaternion.Slerp(SteeringWheel.localRotation, targetSteering, 1f - Mathf.Exp(-SmoothingSpeed * dt));
            }

            // 2. Accelerator pedal depression (around local X axis)
            if (AcceleratorPedal != null)
            {
                float throttle = state.CurrentGear == -1 ? input.Brake : input.Throttle;
                Quaternion targetAccel = _baseAccelRot * Quaternion.Euler(throttle * AcceleratorMaxAngle, 0f, 0f);
                AcceleratorPedal.localRotation = Quaternion.Slerp(AcceleratorPedal.localRotation, targetAccel, 1f - Mathf.Exp(-SmoothingSpeed * dt));
            }

            // 3. Brake pedal depression (around local X axis)
            if (BrakePedal != null)
            {
                float brake = state.CurrentGear == -1 ? input.Throttle : input.Brake;
                Quaternion targetBrake = _baseBrakeRot * Quaternion.Euler(brake * BrakeMaxAngle, 0f, 0f);
                BrakePedal.localRotation = Quaternion.Slerp(BrakePedal.localRotation, targetBrake, 1f - Mathf.Exp(-SmoothingSpeed * dt));
            }

            // 4. Handbrake lever pull (around local X axis)
            if (HandbrakeLever != null)
            {
                float handbrake = input.Handbrake;
                Quaternion targetHandbrake = _baseHandbrakeRot * Quaternion.Euler(-handbrake * HandbrakeMaxAngle, 0f, 0f);
                HandbrakeLever.localRotation = Quaternion.Slerp(HandbrakeLever.localRotation, targetHandbrake, 1f - Mathf.Exp(-SmoothingSpeed * dt));
            }
        }
    }
}
