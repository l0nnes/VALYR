using System;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Interfaces;
using VALYR.Core.Modules;

namespace VALYR.Modules.Assists
{
    /// <summary>
    /// Модуль антиблокировочной системы тормозов (Anti-lock Braking System / ABS).
    /// Отслеживает критическую пробуксовку колес при торможении (SlipRatio < SlipThreshold)
    /// и импульсно ограничивает тормозной момент (MaxBrakeTorqueLimit), предотвращая юз и сохраняя управляемость.
    /// </summary>
    [Serializable]
    public class AbsModule : VehicleModule, IWheelAssistModule
    {
        [Header("Настройки ABS")]
        [Tooltip("Порог срабатывания по продольному скольжению (обычно -0.15 .. -0.25)")]
        [Range(-0.4f, -0.05f)]
        public float SlipThreshold = -0.18f;

        [Tooltip("Минимальная скорость автомобиля для работы ABS (м/с, ниже этого порога колеса блокируются для полной остановки)")]
        public float MinSpeedThreshold = 2.5f;

        [Tooltip("Коэффициент сброса тормозного давления при срабатывании (0.2..0.6)")]
        [Range(0.1f, 0.8f)]
        public float PressureReleaseRatio = 0.35f;

        public AbsModule()
        {
            ModuleId = "Assists.ABS";
        }

        public void ApplyAssists(
            in VehicleInput input,
            in VehicleState state,
            ref WheelData4<WheelActuation> actuation,
            IPhysicsBody body,
            in VehicleChassisConfig chassis,
            float dt)
        {
            if (!IsEnabled || input.Brake < 0.05f)
                return;

            float speed = body.LinearVelocity.magnitude;
            if (speed < MinSpeedThreshold)
                return;

            for (int i = 0; i < 4; i++)
            {
                WheelState wheel = state.Wheels[i];
                if (!wheel.HasContact)
                    continue;

                // Если продольное скольжение превышает порог блокировки
                if (wheel.SlipRatio < SlipThreshold)
                {
                    ref WheelActuation wheelAct = ref actuation.GetRef(i);
                    float safeLimit = wheelAct.BrakeTorque * PressureReleaseRatio;
                    wheelAct.MaxBrakeTorqueLimit = Mathf.Min(wheelAct.MaxBrakeTorqueLimit, safeLimit);
                }
            }
        }
    }
}
