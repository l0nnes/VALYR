using System;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Interfaces;
using VALYR.Core.Modules;

namespace VALYR.Modules.Assists
{
    /// <summary>
    /// Traction Control System (TCS) module.
    /// Monitors excessive drive wheel spin during acceleration (SlipRatio &gt; SlipThreshold)
    /// and dynamically cuts delivered engine torque (MaxDriveTorqueLimit), preventing
    /// burnout and maintaining directional stability.
    /// </summary>
    [Serializable]
    public class TcsModule : VehicleModule, IWheelAssistModule
    {
        [Header("Traction Control System (TCS)")]
        [Tooltip("Positive longitudinal slip ratio threshold to trigger TCS torque cut (typical: 0.15 to 0.30).")]
        [Range(0.08f, 0.5f)]
        public float SlipThreshold = 0.20f;

        [Tooltip("Drive torque delivery ratio during TCS intervention (0.1 to 0.6, fraction of engine torque delivered to slipping wheel).")]
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

                // If wheel exceeds positive slip threshold (wheel spinning under throttle)
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
