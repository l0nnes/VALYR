using System;
using UnityEngine;

namespace VALYR.Core
{
    /// <summary>
    /// Actuation buffer containing drive and braking torques applied to a single wheel in the current tick.
    /// Populated by transmission and brakes, then modulated by electronic driver assists (ABS, TCS).
    /// </summary>
    [Serializable]
    public struct WheelActuation
    {
        [Tooltip("Drive torque delivered from engine/transmission in Newton-meters (N*m).")]
        public float DriveTorque;

        [Tooltip("Brake torque commanded by driver service footbrake in Newton-meters (N*m).")]
        public float BrakeTorque;

        [Tooltip("Brake torque commanded by parking / handbrake in Newton-meters (N*m).")]
        public float HandbrakeTorque;

        [Tooltip("Maximum drive torque limit enforced by TCS assist (float.MaxValue = unconstrained).")]
        public float MaxDriveTorqueLimit;

        [Tooltip("Maximum brake torque limit enforced by ABS assist (float.MaxValue = unconstrained).")]
        public float MaxBrakeTorqueLimit;

        public void Reset(float maxLimit = float.MaxValue)
        {
            DriveTorque = 0f;
            BrakeTorque = 0f;
            HandbrakeTorque = 0f;
            MaxDriveTorqueLimit = maxLimit;
            MaxBrakeTorqueLimit = maxLimit;
        }

        public float GetEffectiveDriveTorque()
        {
            return Mathf.Min(DriveTorque, MaxDriveTorqueLimit);
        }

        public float GetEffectiveBrakeTorque()
        {
            float requestedBrake = BrakeTorque + HandbrakeTorque;
            return Mathf.Min(requestedBrake, MaxBrakeTorqueLimit);
        }
    }
}
