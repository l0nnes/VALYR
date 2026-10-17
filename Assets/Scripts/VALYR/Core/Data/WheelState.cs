using System;
using UnityEngine;

namespace VALYR.Core
{
    /// <summary>
    /// Complete physics state of a single wheel at the start/end of a simulation tick.
    /// Stores kinematics, suspension compression, surface contact data, and tire forces.
    /// </summary>
    [Serializable]
    public struct WheelState
    {
        // --- Wheel Rotational Kinematics ---
        [Tooltip("Rotational angular velocity of the wheel hub in rad/s.")]
        public float AngularVelocity;

        [Tooltip("Accumulated visual roll rotation angle of the wheel in radians.")]
        public float RotationAngle;

        [Tooltip("Current steer angle of the wheel around the vertical axis in radians.")]
        public float SteerAngle;

        // --- Suspension & Ground Contact ---
        [Tooltip("Indicates whether the wheel is currently touching a ground surface.")]
        public bool HasContact;

        [Tooltip("World position of the ground contact patch.")]
        public Vector3 ContactPoint;

        [Tooltip("Surface contact normal vector in world space.")]
        public Vector3 ContactNormal;

        [Tooltip("Suspension compression travel in meters (0 = fully extended).")]
        public float SuspensionCompression;

        [Tooltip("Suspension velocity along stroke in m/s (+ compression, - rebound).")]
        public float SuspensionVelocity;

        [Tooltip("Total vertical normal load on the tire contact patch in Newtons (Fz).")]
        public float NormalLoad;

        // --- Tire Forces & Slip Dynamics ---
        [Tooltip("Longitudinal tire slip ratio (sx, dimensionless).")]
        public float SlipRatio;

        [Tooltip("Lateral tire slip angle / ratio (sy, dimensionless).")]
        public float SlipAngle;

        [Tooltip("Resultant 3D tire friction force applied to vehicle in world space (Newtons).")]
        public Vector3 TireForce;

        [Tooltip("Normalized skid intensity 0..1 for audio squeal, tire smoke, and skid mark VFX.")]
        public float SkidIntensity;
    }
}
