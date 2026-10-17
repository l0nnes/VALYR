using System;
using UnityEngine;
using VALYR.Core;

namespace VALYR.Runtime.Visual
{
    /// <summary>
    /// Слой визуализации колес автомобиля (Visual Presentation Layer).
    /// Вызывается в Update/LateUpdate для плавного рендеринга без дрожания:
    /// - Смещает меши колес по вертикали на величину сжатия подвески
    /// - Поворачивает колеса вокруг вертикальной оси (SteerAngle)
    /// - Вращает колеса вокруг оси вращения ступицы (RotationAngle)
    /// </summary>
    public class VehicleVisualView : MonoBehaviour
    {
        [Header("Визуальные меши колес (FL, FR, RL, RR)")]
        public Transform FrontLeftWheel;
        public Transform FrontRightWheel;
        public Transform RearLeftWheel;
        public Transform RearRightWheel;

        [Header("Точки крепления подвески (Anchors)")]
        [Tooltip("Необязательные родительские точки крепления подвески")]
        public Transform FL_SuspensionAnchor;
        public Transform FR_SuspensionAnchor;
        public Transform RL_SuspensionAnchor;
        public Transform RR_SuspensionAnchor;

        [Header("Настройки подвески для визуала")]
        [Tooltip("Длина подвески в покое (метры, расстояние от точки крепления до оси ступицы)")]
        public float SuspensionRestLength = 0.345f;

        [Tooltip("Скорость сглаживания вертикального движения колес (0 = мгновенно без сглаживания)")]
        public float SuspensionPositionSmoothRate = 25f;

        // Кэшированные начальные локальные позиции
        private Vector3[] _baseLocalPositions;
        private Vector3[] _currentLocalPositions;
        private Transform[] _wheels;
        private Transform[] _anchors;

        private void Reset()
        {
            AutoFindReferences();
        }

        private void OnValidate()
        {
            if (FrontLeftWheel == null || FrontRightWheel == null || RearLeftWheel == null || RearRightWheel == null)
            {
                AutoFindReferences();
            }
        }

        public void AutoFindReferences()
        {
            if (FrontLeftWheel == null) FrontLeftWheel = transform.Find("Wheels/Meshes/FrontLeftWheel");
            if (FrontRightWheel == null) FrontRightWheel = transform.Find("Wheels/Meshes/FrontRightWheel");
            if (RearLeftWheel == null) RearLeftWheel = transform.Find("Wheels/Meshes/RearLeftWheel");
            if (RearRightWheel == null) RearRightWheel = transform.Find("Wheels/Meshes/RearRightWheel");

            if (FL_SuspensionAnchor == null) FL_SuspensionAnchor = transform.Find("Suspension/FL_Suspension");
            if (FR_SuspensionAnchor == null) FR_SuspensionAnchor = transform.Find("Suspension/FR_Suspension");
            if (RL_SuspensionAnchor == null) RL_SuspensionAnchor = transform.Find("Suspension/RL_Suspension");
            if (RR_SuspensionAnchor == null) RR_SuspensionAnchor = transform.Find("Suspension/RR_Suspension");
            SuspensionRestLength = 0.345f;
        }

        private void Awake()
        {
            if (FrontLeftWheel == null || FrontRightWheel == null || RearLeftWheel == null || RearRightWheel == null)
            {
                AutoFindReferences();
            }

            _wheels = new Transform[4]
            {
                FrontLeftWheel,
                FrontRightWheel,
                RearLeftWheel,
                RearRightWheel
            };

            _anchors = new Transform[4]
            {
                FL_SuspensionAnchor,
                FR_SuspensionAnchor,
                RL_SuspensionAnchor,
                RR_SuspensionAnchor
            };

            _baseLocalPositions = new Vector3[4];
            _currentLocalPositions = new Vector3[4];

            for (int i = 0; i < 4; i++)
            {
                if (_wheels[i] != null)
                {
                    _baseLocalPositions[i] = _wheels[i].localPosition;
                    _currentLocalPositions[i] = _baseLocalPositions[i];
                }
            }
        }

        /// <summary>
        /// Обновляет позиции и углы ориентации всех 4 колес на основе текущего состояния симуляции.
        /// </summary>
        public void UpdateWheelsView(in VehicleState state, float dt)
        {
            for (int i = 0; i < 4; i++)
            {
                Transform wheelTransform = _wheels[i];
                if (wheelTransform == null)
                    continue;

                WheelState wheelState = state.Wheels[i];

                // 1. Позиция по высоте подвески
                float compression = wheelState.SuspensionCompression;
                Vector3 basePos = _baseLocalPositions[i];

                // Если задан Transform анкера подвески, рассчитываем относительно него
                if (_anchors[i] != null)
                {
                    // Анкер в координатах родителя колеса
                    Vector3 anchorPos = wheelTransform.parent.InverseTransformPoint(_anchors[i].position);
                    float targetY = anchorPos.y - (SuspensionRestLength - compression);
                    basePos = new Vector3(anchorPos.x, targetY, anchorPos.z);
                }
                else
                {
                    // Относительно начальной сохраненной высоты
                    basePos.y = _baseLocalPositions[i].y + compression;
                }

                if (SuspensionPositionSmoothRate > 0.01f && dt > 0f)
                {
                    _currentLocalPositions[i] = Vector3.Lerp(
                        _currentLocalPositions[i],
                        basePos,
                        1f - Mathf.Exp(-SuspensionPositionSmoothRate * dt));
                }
                else
                {
                    _currentLocalPositions[i] = basePos;
                }

                wheelTransform.localPosition = _currentLocalPositions[i];

                // 2. Вращение колеса (Steering по Y + Roll по X)
                float steerDeg = wheelState.SteerAngle * Mathf.Rad2Deg;
                float rollDeg = wheelState.RotationAngle * Mathf.Rad2Deg;

                Quaternion steerRotation = Quaternion.Euler(0f, steerDeg, 0f);
                Quaternion rollRotation = Quaternion.Euler(rollDeg, 0f, 0f);

                wheelTransform.localRotation = steerRotation * rollRotation;
            }
        }
    }
}
