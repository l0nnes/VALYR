using System;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Interfaces;
using VALYR.Core.Modules;

namespace VALYR.Modules.Steering
{
    /// <summary>
    /// Модуль рулевого управления передней осью (Direct Steering Module).
    /// Поддерживает чувствительность к скорости (speed-sensitive steering) для устойчивости на трассе
    /// и геометрию Аккермана (Ackermann Steering Geometry) для идеального радиуса поворота внутренних и внешних колес.
    /// </summary>
    [Serializable]
    public class DirectSteeringModule : VehicleModule, ISteeringModule
    {
        [Header("Углы поворота колес")]
        [Tooltip("Максимальный угол поворота передних колес на парковочных скоростях (градусы)")]
        [Range(15f, 50f)]
        public float MaxSteerAngleDeg = 36f;

        [Header("Чувствительность к скорости")]
        [Tooltip("Включить автоматическое уменьшение угла руля с ростом скорости для курсовой устойчивости")]
        public bool EnableSpeedSensitivity = true;

        [Tooltip("Минимальный максимальный угол руля на высокой скорости (градусы)")]
        [Range(5f, 25f)]
        public float MinSteerAngleAtHighSpeedDeg = 12f;

        [Tooltip("Скорость автомобиля, при которой достигается минимальный угол поворота (м/с, 40 м/с ~ 144 км/ч)")]
        public float HighSpeedThreshold = 40f;

        [Header("Динамика руления")]
        [Tooltip("Скорость отклика рейки рулевого механизма (1/с, чем выше — тем острее отклик)")]
        public float SteeringResponseRate = 16f;

        [Tooltip("Использовать кинематику Аккермана (внутреннее колесо поворачивает круче внешнего)")]
        public bool UseAckermann = true;

        public DirectSteeringModule()
        {
            ModuleId = "Steering.Direct";
        }

        public void UpdateSteering(
            in VehicleInput input,
            ref VehicleState state,
            IPhysicsBody body,
            in VehicleChassisConfig chassis,
            float dt)
        {
            if (!IsEnabled)
                return;

            float steerInput = Mathf.Clamp(input.Steer, -1f, 1f);

            // 1. Определение эффективного максимального угла с учетом скорости машины
            float forwardSpeed = Vector3.Dot(body.LinearVelocity, body.Rotation * Vector3.forward);
            float speedAbs = Mathf.Abs(forwardSpeed);

            float currentMaxAngleDeg = MaxSteerAngleDeg;
            if (EnableSpeedSensitivity && HighSpeedThreshold > 0.01f)
            {
                float speedFactor = Mathf.Clamp01(speedAbs / HighSpeedThreshold);
                // Плавное квадратичное уменьшение угла руля
                currentMaxAngleDeg = Mathf.Lerp(MaxSteerAngleDeg, MinSteerAngleAtHighSpeedDeg, speedFactor * (2f - speedFactor));
            }

            // Целевой угол в радианах
            float targetSteerRad = (steerInput * currentMaxAngleDeg) * Mathf.Deg2Rad;

            // Сглаживание движения рейки руля
            float currentSteerRad = (state.Wheels[0].SteerAngle + state.Wheels[1].SteerAngle) * 0.5f;
            float smoothSteerRad = Mathf.Lerp(currentSteerRad, targetSteerRad, 1f - Mathf.Exp(-SteeringResponseRate * dt));

            float steerAngleFL;
            float steerAngleFR;

            // 2. Геометрия Аккермана
            if (UseAckermann && Mathf.Abs(smoothSteerRad) > 0.001f)
            {
                // База автомобиля (Wheelbase L) и колея (Track W)
                float wheelbase = Mathf.Abs(chassis.WheelOffsets[0].z - chassis.WheelOffsets[2].z);
                float trackWidth = Mathf.Abs(chassis.WheelOffsets[1].x - chassis.WheelOffsets[0].x);

                if (wheelbase > 0.1f && trackWidth > 0.1f)
                {
                    float turnRadius = wheelbase / Mathf.Tan(Mathf.Abs(smoothSteerRad));

                    if (smoothSteerRad > 0f) // Поворот вправо: правое FR внутреннее, левое FL внешнее
                    {
                        steerAngleFR = Mathf.Atan(wheelbase / (turnRadius - trackWidth * 0.5f));
                        steerAngleFL = Mathf.Atan(wheelbase / (turnRadius + trackWidth * 0.5f));
                    }
                    else // Поворот влево: левое FL внутреннее, правое FR внешнее
                    {
                        steerAngleFL = -Mathf.Atan(wheelbase / (turnRadius - trackWidth * 0.5f));
                        steerAngleFR = -Mathf.Atan(wheelbase / (turnRadius + trackWidth * 0.5f));
                    }
                }
                else
                {
                    steerAngleFL = smoothSteerRad;
                    steerAngleFR = smoothSteerRad;
                }
            }
            else
            {
                // Параллельное руление
                steerAngleFL = smoothSteerRad;
                steerAngleFR = smoothSteerRad;
            }

            // Запись в передние колеса (FL, FR)
            state.Wheels.GetRef(0).SteerAngle = steerAngleFL;
            state.Wheels.GetRef(1).SteerAngle = steerAngleFR;

            // Задние колеса фиксированы
            state.Wheels.GetRef(2).SteerAngle = 0f;
            state.Wheels.GetRef(3).SteerAngle = 0f;
        }
    }
}
