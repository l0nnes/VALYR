using System;
using UnityEngine;

namespace VALYR.Core
{
    /// <summary>
    /// Immutable input data representing driver / AI commands for a single fixed simulation tick.
    /// Blittable struct designed for direct serialization and network prediction (PurrDiction IPredictedData).
    /// </summary>
    [Serializable]
    public struct VehicleInput
    {
        [Tooltip("Throttle pedal input normalized to [0, 1].")]
        [Range(0f, 1f)]
        public float Throttle;

        [Tooltip("Steering wheel / axis input normalized to [-1, +1] (-1 = full left, +1 = full right).")]
        [Range(-1f, 1f)]
        public float Steer;

        [Tooltip("Service footbrake pedal input normalized to [0, 1].")]
        [Range(0f, 1f)]
        public float Brake;

        [Tooltip("Parking / handbrake command normalized to [0, 1].")]
        [Range(0f, 1f)]
        public float Handbrake;

        public VehicleInput(float throttle, float steer, float brake, float handbrake)
        {
            Throttle = Mathf.Clamp01(throttle);
            Steer = Mathf.Clamp(steer, -1f, 1f);
            Brake = Mathf.Clamp01(brake);
            Handbrake = Mathf.Clamp01(handbrake);
        }

        public void Sanitize()
        {
            Throttle = Mathf.Clamp01(Throttle);
            Steer = Mathf.Clamp(Steer, -1f, 1f);
            Brake = Mathf.Clamp01(Brake);
            Handbrake = Mathf.Clamp01(Handbrake);
        }

        public void Dispose() { }
    }
}
