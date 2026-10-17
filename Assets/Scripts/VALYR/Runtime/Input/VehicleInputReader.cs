using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VALYR.Core;

namespace VALYR.Runtime.Input
{
    /// <summary>
    /// Vehicle driver input reader built on top of the Unity Input System.
    /// Supports keyboard (WASD / Arrow keys / Space) and gamepad (analog triggers/stick),
    /// with smooth non-linear filtering for keyboard digital inputs.
    /// </summary>
    [Serializable]
    public class VehicleInputReader : MonoBehaviour
    {
        [Header("Input Devices")]
        [Tooltip("Enable input polling from keyboard.")]
        public bool EnableKeyboard = true;

        [Tooltip("Enable input polling from gamepad.")]
        public bool EnableGamepad = true;

        [Header("Digital Keyboard Smoothing")]
        [Tooltip("Steering wheel angle build-up rate when holding turn key (1/s, typical: 3.0 - 6.0).")]
        [Range(1f, 15f)]
        public float SteerAttackSpeed = 4.0f;

        [Tooltip("Steering return-to-center rate upon key release (1/s, typical: 4.0 - 10.0).")]
        [Range(1f, 20f)]
        public float SteerReturnSpeed = 6.0f;

        [Tooltip("Throttle pedal build-up rate upon key press (1/s, typical: 5.0 - 12.0).")]
        [Range(1f, 25f)]
        public float ThrottleAttackSpeed = 8.0f;

        [Tooltip("Footbrake pedal build-up rate upon key press (1/s, typical: 6.0 - 15.0).")]
        [Range(1f, 25f)]
        public float BrakeAttackSpeed = 10.0f;

        // Internal smoothed values
        private float _smoothThrottle;
        private float _smoothSteer;
        private float _smoothBrake;
        private float _smoothHandbrake;

        public VehicleInput CurrentInput { get; private set; }

        public VehicleInput ReadInput(float dt)
        {
            float targetThrottle = 0f;
            float targetSteer = 0f;
            float targetBrake = 0f;
            float targetHandbrake = 0f;

            // 1. Keyboard polling
            if (EnableKeyboard && Keyboard.current != null)
            {
                var kb = Keyboard.current;

                if (kb.wKey.isPressed || kb.upArrowKey.isPressed)
                    targetThrottle += 1f;

                if (kb.sKey.isPressed || kb.downArrowKey.isPressed)
                    targetBrake += 1f;

                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)
                    targetSteer -= 1f;

                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed)
                    targetSteer += 1f;

                if (kb.spaceKey.isPressed)
                    targetHandbrake += 1f;
            }

            // 2. Gamepad polling
            if (EnableGamepad && Gamepad.current != null)
            {
                var gp = Gamepad.current;

                // Right trigger: throttle
                float rt = gp.rightTrigger.ReadValue();
                if (rt > 0.05f)
                    targetThrottle = Mathf.Max(targetThrottle, rt);

                // Left trigger: brake
                float lt = gp.leftTrigger.ReadValue();
                if (lt > 0.05f)
                    targetBrake = Mathf.Max(targetBrake, lt);

                // Left stick: steering
                float stickX = gp.leftStick.x.ReadValue();
                if (Mathf.Abs(stickX) > 0.08f)
                    targetSteer = stickX;

                // Button A (South) or Right Shoulder: handbrake
                if (gp.buttonSouth.isPressed || gp.rightShoulder.isPressed)
                    targetHandbrake = 1f;
            }

            // 3. Smooth digital inputs towards targets
            if (Mathf.Abs(targetSteer) > 0.001f)
            {
                _smoothSteer = Mathf.MoveTowards(_smoothSteer, targetSteer, SteerAttackSpeed * dt);
            }
            else
            {
                _smoothSteer = Mathf.MoveTowards(_smoothSteer, 0f, SteerReturnSpeed * dt);
            }

            _smoothThrottle = Mathf.MoveTowards(_smoothThrottle, targetThrottle, ThrottleAttackSpeed * dt);
            _smoothBrake = Mathf.MoveTowards(_smoothBrake, targetBrake, BrakeAttackSpeed * dt);
            _smoothHandbrake = Mathf.MoveTowards(_smoothHandbrake, targetHandbrake, 15f * dt);

            CurrentInput = new VehicleInput
            {
                Throttle = Mathf.Clamp01(_smoothThrottle),
                Steer = Mathf.Clamp(_smoothSteer, -1f, 1f),
                Brake = Mathf.Clamp01(_smoothBrake),
                Handbrake = Mathf.Clamp01(_smoothHandbrake)
            };

            return CurrentInput;
        }

        public void ResetInput()
        {
            _smoothThrottle = 0f;
            _smoothSteer = 0f;
            _smoothBrake = 0f;
            _smoothHandbrake = 0f;
            CurrentInput = default;
        }
    }
}
