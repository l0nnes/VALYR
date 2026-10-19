using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VALYR.Core;

namespace VALYR.Runtime.Input
{
    /// <summary>
    /// Vehicle driver input reader built on top of the Unity New Input System (Input Actions).
    /// Reads from the "Driving" ActionMap (Throttle/Brake, Steering, Handbrake, Gearbox, Clutch),
    /// supporting Keyboard, Gamepad, and steering wheels with smooth progressive filtering.
    /// </summary>
    [Serializable]
    public class VehicleInputReader : MonoBehaviour
    {
        [Header("New Input System Actions")]
        [Tooltip("Input Actions asset (e.g. GameInput). Can be assigned in Inspector or loaded automatically.")]
        [SerializeField] private InputActionAsset _inputActions;

        [Tooltip("Action map name for driving controls.")]
        [SerializeField] private string _actionMapName = "Driving";

        [Tooltip("1D Axis action name (-1 = Brake, +1 = Throttle).")]
        [SerializeField] private string _throttleBrakeActionName = "Throttle / Brake";

        [Tooltip("1D Axis action name (-1 = Left, +1 = Right).")]
        [SerializeField] private string _steeringActionName = "Steering";

        [Tooltip("Button action name for handbrake.")]
        [SerializeField] private string _handbrakeActionName = "Handbrake";

        [Tooltip("Button action name for shifting gear up.")]
        [SerializeField] private string _gearUpActionName = "Gearbox Up";

        [Tooltip("Button action name for shifting gear down.")]
        [SerializeField] private string _gearDownActionName = "Gearbox Down";

        [Tooltip("Button action name for clutch.")]
        [SerializeField] private string _clutchActionName = "Clutch";

        [Header("Fallback Polling Options")]
        [Tooltip("Enable direct hardware polling fallback if Input Action is missing.")]
        public bool EnableFallback = true;

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

        // Resolved Action Map and Actions
        private InputActionMap _actionMap;
        private InputAction _throttleBrakeAction;
        private InputAction _steeringAction;
        private InputAction _handbrakeAction;
        private InputAction _gearUpAction;
        private InputAction _gearDownAction;
        private InputAction _clutchAction;

        // Internal smoothed values
        private float _smoothThrottle;
        private float _smoothSteer;
        private float _smoothBrake;
        private float _smoothHandbrake;

        public VehicleInput CurrentInput { get; private set; }
        public InputActionAsset InputActions => _inputActions;
        public bool ShiftUpTriggered => _gearUpAction?.WasPressedThisFrame() ?? false;
        public bool ShiftDownTriggered => _gearDownAction?.WasPressedThisFrame() ?? false;
        public float ClutchValue => _clutchAction?.ReadValue<float>() ?? 0f;

        public void SetInputActionsAsset(InputActionAsset asset)
        {
            if (_inputActions != null && _actionMap != null)
            {
                _actionMap.Disable();
            }

            _inputActions = asset;
            InitializeInputActions();
        }

        private void Awake()
        {
            InitializeInputActions();
        }

        private void OnEnable()
        {
            if (_actionMap != null && !_actionMap.enabled)
            {
                _actionMap.Enable();
            }
        }

        private void OnDisable()
        {
            _actionMap?.Disable();
        }

        private void InitializeInputActions()
        {
            if (_inputActions == null)
            {
#if UNITY_EDITOR
                // Auto-load GameInput.inputactions if not explicitly wired in inspector
                _inputActions = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/GameInput.inputactions");
#endif
            }

            if (_inputActions != null)
            {
                _actionMap = _inputActions.FindActionMap(_actionMapName);
                if (_actionMap != null)
                {
                    _throttleBrakeAction = _actionMap.FindAction(_throttleBrakeActionName);
                    _steeringAction = _actionMap.FindAction(_steeringActionName);
                    _handbrakeAction = _actionMap.FindAction(_handbrakeActionName);
                    _gearUpAction = _actionMap.FindAction(_gearUpActionName);
                    _gearDownAction = _actionMap.FindAction(_gearDownActionName);
                    _clutchAction = _actionMap.FindAction(_clutchActionName);

                    if (isActiveAndEnabled)
                    {
                        _actionMap.Enable();
                    }
                }
            }
        }

        public VehicleInput ReadInput(float dt)
        {
            float targetThrottle = 0f;
            float targetSteer = 0f;
            float targetBrake = 0f;
            float targetHandbrake = 0f;
            bool actionsRead = false;

            // 1. Read from Unity New Input System Actions
            if (_actionMap != null && _actionMap.enabled)
            {
                if (_throttleBrakeAction != null)
                {
                    float tb = _throttleBrakeAction.ReadValue<float>();
                    if (tb > 0.001f)
                    {
                        targetThrottle = tb;
                    }
                    else if (tb < -0.001f)
                    {
                        targetBrake = -tb;
                    }
                    actionsRead = true;
                }

                if (_steeringAction != null)
                {
                    targetSteer = _steeringAction.ReadValue<float>();
                    actionsRead = true;
                }

                if (_handbrakeAction != null)
                {
                    targetHandbrake = _handbrakeAction.IsPressed() ? 1f : 0f;
                    actionsRead = true;
                }
            }

            // 2. Hardware fallback polling if actions not active
            if (!actionsRead && EnableFallback)
            {
                if (Keyboard.current != null)
                {
                    var kb = Keyboard.current;
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) targetThrottle += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) targetBrake += 1f;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) targetSteer -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) targetSteer += 1f;
                    if (kb.spaceKey.isPressed) targetHandbrake += 1f;
                }

                if (Gamepad.current != null)
                {
                    var gp = Gamepad.current;
                    float rt = gp.rightTrigger.ReadValue();
                    if (rt > 0.05f) targetThrottle = Mathf.Max(targetThrottle, rt);

                    float lt = gp.leftTrigger.ReadValue();
                    if (lt > 0.05f) targetBrake = Mathf.Max(targetBrake, lt);

                    float stickX = gp.leftStick.x.ReadValue();
                    if (Mathf.Abs(stickX) > 0.08f) targetSteer = stickX;

                    if (gp.buttonSouth.isPressed || gp.rightShoulder.isPressed) targetHandbrake = 1f;
                }
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
