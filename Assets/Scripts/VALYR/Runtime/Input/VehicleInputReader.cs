using System;
using UnityEngine;
using UnityEngine.InputSystem;
using VALYR.Core;

namespace VALYR.Runtime.Input
{
    /// <summary>
    /// Считыватель команд управления автомобиля на базе нового Unity Input System.
    /// Поддерживает клавиатуру (WASD / стрелки / пробел) и геймпад с плавным сглаживанием
    /// аналогового отклика для клавиатуры.
    /// </summary>
    [Serializable]
    public class VehicleInputReader : MonoBehaviour
    {
        [Header("Устройства ввода")]
        [Tooltip("Включить считывание с клавиатуры")]
        public bool EnableKeyboard = true;

        [Tooltip("Включить считывание с геймпада")]
        public bool EnableGamepad = true;

        [Header("Сглаживание клавишного ввода")]
        [Tooltip("Скорость нарастания поворота руля при нажатии клавиши (1/с)")]
        public float SteerAttackSpeed = 4.0f;

        [Tooltip("Скорость возврата руля в центр при отпускании клавиши (1/с)")]
        public float SteerReturnSpeed = 6.0f;

        [Tooltip("Скорость нарастания нажатия педали газа (1/с)")]
        public float ThrottleAttackSpeed = 8.0f;

        [Tooltip("Скорость нарастания нажатия педали тормоза (1/с)")]
        public float BrakeAttackSpeed = 10.0f;

        // Внутренние сглаженные значения
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

            // 1. Считывание с клавиатуры
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

            // 2. Считывание с геймпада
            if (EnableGamepad && Gamepad.current != null)
            {
                var gp = Gamepad.current;

                // Правый триггер — газ
                float rt = gp.rightTrigger.ReadValue();
                if (rt > 0.05f)
                    targetThrottle = Mathf.Max(targetThrottle, rt);

                // Левый триггер — тормоз
                float lt = gp.leftTrigger.ReadValue();
                if (lt > 0.05f)
                    targetBrake = Mathf.Max(targetBrake, lt);

                // Левый стик — руль
                float stickX = gp.leftStick.x.ReadValue();
                if (Mathf.Abs(stickX) > 0.08f)
                    targetSteer = stickX;

                // Кнопка A (South) или правый бампер — ручной тормоз
                if (gp.buttonSouth.isPressed || gp.rightShoulder.isPressed)
                    targetHandbrake = 1f;
            }

            // 3. Плавная фильтрация ввода
            // Руль:
            if (Mathf.Abs(targetSteer) > 0.001f)
            {
                _smoothSteer = Mathf.MoveTowards(_smoothSteer, targetSteer, SteerAttackSpeed * dt);
            }
            else
            {
                _smoothSteer = Mathf.MoveTowards(_smoothSteer, 0f, SteerReturnSpeed * dt);
            }

            // Газ:
            _smoothThrottle = Mathf.MoveTowards(_smoothThrottle, targetThrottle, ThrottleAttackSpeed * dt);

            // Тормоз:
            _smoothBrake = Mathf.MoveTowards(_smoothBrake, targetBrake, BrakeAttackSpeed * dt);

            // Ручник:
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
