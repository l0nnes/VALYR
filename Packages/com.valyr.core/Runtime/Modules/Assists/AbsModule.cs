using System;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Interfaces;
using VALYR.Core.Modules;

namespace VALYR.Modules.Assists
{
    /// <summary>
    /// Anti-lock Braking System (ABS) module.
    /// Monitors longitudinal wheel slip during braking (SlipRatio &lt; SlipThreshold)
    /// and dynamically modulates brake torque limits (MaxBrakeTorqueLimit), preventing lockup
    /// and preserving vehicle steerability under hard braking.
    /// </summary>
    [Serializable]
    public class AbsModule : VehicleModule, IWheelAssistModule
    {
        [Header("Anti-Lock Braking System (ABS)")]
        [Tooltip("Longitudinal slip threshold to trigger ABS intervention (typical: -0.15 to -0.25). More negative values allow more initial tire lockup.")]
        [Range(-0.4f, -0.05f)]
        public float SlipThreshold = -0.18f;

        [Tooltip("Minimum vehicle speed in m/s below which ABS disengages to allow full lockup for standstill (typical: 2.0 - 3.0 m/s).")]
        [Range(0.5f, 5.0f)]
        public float MinSpeedThreshold = 2.5f;

        [Tooltip("Hydraulic brake pressure reduction ratio during ABS pulse (0.1 to 0.6, fraction of driver brake torque maintained).")]
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

                // If wheel exceeds negative slip threshold (wheel locking up under braking)
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
