using System;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Interfaces;
using VALYR.Core.Modules;

namespace VALYR.Modules.Assists
{
    /// <summary>
    /// Модуль антипробуксовочной системы (Traction Control System / TCS).
    /// Отслеживает избыточную пробуксовку ведущих колес при ускорении (SlipRatio > SlipThreshold)
    /// и ограничивает подаваемый крутящий момент (MaxDriveTorqueLimit), предотвращая срыв в занос и шлифовку асфальта.
    /// </summary>
    [Serializable]
    public class TcsModule : VehicleModule, IWheelAssistModule
    {
        [Header("Настройки TCS")]
        [Tooltip("Порог срабатывания по продольной пробуксовке (обычно 0.15 .. 0.28)")]
        [Range(0.08f, 0.5f)]
        public float SlipThreshold = 0.20f;

        [Tooltip("Коэффициент урезания тяги при срабатывании TCS (0.1..0.6)")]
        [Range(0.05f, 0.7f)]
        public float TorqueReductionRatio = 0.35f;

        public TcsModule()
        {
            ModuleId = "Assists.TCS";
        }

        public void ApplyAssists(
            in VehicleInput input,
            in VehicleState state,
            ref WheelData4<WheelActuation> actuation,
            IPhysicsBody body,
            in VehicleChassisConfig chassis,
            float dt)
        {
            if (!IsEnabled || input.Throttle < 0.05f)
                return;

            for (int i = 0; i < 4; i++)
            {
                WheelState wheel = state.Wheels[i];
                if (!wheel.HasContact)
                    continue;

                // Если продольная пробуксовка превышает порог срыва
                if (wheel.SlipRatio > SlipThreshold)
                {
                    ref WheelActuation wheelAct = ref actuation.GetRef(i);
                    float safeLimit = wheelAct.DriveTorque * TorqueReductionRatio;
                    wheelAct.MaxDriveTorqueLimit = Mathf.Min(wheelAct.MaxDriveTorqueLimit, safeLimit);
                }
            }
        }
    }
}
