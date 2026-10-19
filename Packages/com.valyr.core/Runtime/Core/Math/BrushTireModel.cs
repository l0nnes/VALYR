using System;
using UnityEngine;

namespace VALYR.Core.Math
{
    /// <summary>
    /// Physical configuration settings for the Brush Tire Model (Fiala / Pacejka).
    /// </summary>
    [Serializable]
    public struct BrushTireParameters
    {
        [Tooltip("Longitudinal tire tread stiffness in Newtons per unit slip (N/slip, Cx, typical: 20,000 - 45,000 N).")]
        [Min(1000f)]
        public float LongitudinalStiffness;

        [Tooltip("Cornering / lateral stiffness of tire tread in Newtons per radian (N/rad, Cy, typical: 15,000 - 40,000 N/rad).")]
        [Min(1000f)]
        public float LateralStiffness;

        [Tooltip("Peak longitudinal friction coefficient (mu_x, typical: 0.95 - 1.15 on dry asphalt, 0.60 on wet).")]
        [Range(0.1f, 2.5f)]
        public float FrictionCoefficientX;

        [Tooltip("Peak lateral friction coefficient (mu_y, typical: 0.90 - 1.10 on dry asphalt, 0.55 on wet).")]
        [Range(0.1f, 2.5f)]
        public float FrictionCoefficientY;

        [Tooltip("Low-speed denominator threshold in m/s to prevent division by zero in slip formulation (typical: 0.3 - 0.8 m/s).")]
        [Range(0.1f, 2.0f)]
        public float LowSpeedThreshold;

        public static BrushTireParameters Default => new()
        {
            LongitudinalStiffness = 22000f,
            LateralStiffness = 18000f,
            FrictionCoefficientX = 1.00f,
            FrictionCoefficientY = 0.95f,
            LowSpeedThreshold = 0.5f
        };
    }

    /// <summary>
    /// High-performance analytical Brush Tire Model (Fiala / Pacejka hybrid).
    /// Features:
    /// 1. Smooth combined slip friction ellipse (longitudinal + lateral coupling).
    /// 2. True static and kinematic friction without artificial low-speed zeroing.
    /// 3. Symmetric contact patch velocity denominator.
    /// 4. Zero heap allocations.
    /// </summary>
    public static class BrushTireModel
    {
        private const float Epsilon = 1e-5f;

        /// <summary>
        /// Computes longitudinal slip ratio (sx) and lateral slip angle / slip ratio (sy).
        /// </summary>
        public static void CalculateSlip(
            float angularVelocity,
            float wheelRadius,
            float forwardSpeed,
            float sideSpeed,
            out float slipRatio,
            out float slipAngle)
        {
            float linearWheelSpeed = angularVelocity * wheelRadius;
            float contactSpeed = UnityEngine.Mathf.Sqrt(forwardSpeed * forwardSpeed + sideSpeed * sideSpeed);
            float denominator = UnityEngine.Mathf.Max(contactSpeed, 0.5f);

            // sx = (omega * R - Vx) / max(V_contact, V_min)
            slipRatio = (linearWheelSpeed - forwardSpeed) / denominator;

            // sy = -Vy / max(V_contact, V_min) (tangent of slip angle)
            slipAngle = -sideSpeed / denominator;
        }

        /// <summary>
        /// Calculates coupled longitudinal (Fx) and lateral (Fy) tire contact forces.
        /// </summary>
        public static void CalculateTireForces(
            in BrushTireParameters parameters,
            float normalLoad,
            float slipRatio,
            float slipAngle,
            float surfaceFrictionMultiplier,
            out float fx,
            out float fy,
            out float skidIntensity)
        {
            // If wheel has no normal load or is off the ground, contact forces are zero
            if (normalLoad <= Epsilon)
            {
                fx = 0f;
                fy = 0f;
                skidIntensity = 0f;
                return;
            }

            float effectiveMuX = UnityEngine.Mathf.Max(parameters.FrictionCoefficientX * surfaceFrictionMultiplier, 0.1f);
            float effectiveMuY = UnityEngine.Mathf.Max(parameters.FrictionCoefficientY * surfaceFrictionMultiplier, 0.1f);

            float maxFx = effectiveMuX * normalLoad;
            float maxFy = effectiveMuY * normalLoad;

            // Dimensionless bristle elastic deformation components
            float sigmaX = (parameters.LongitudinalStiffness * slipRatio) / maxFx;
            float sigmaY = (parameters.LateralStiffness * slipAngle) / maxFy;

            // Combined resultant slip vector
            float sigma = UnityEngine.Mathf.Sqrt(sigmaX * sigmaX + sigmaY * sigmaY);

            if (sigma < Epsilon)
            {
                fx = 0f;
                fy = 0f;
                skidIntensity = 0f;
                return;
            }

            // theta = 1/3 * sigma. Full sliding occurs when theta >= 1.0 (sigma >= 3.0)
            float theta = sigma * (1f / 3f);
            float fn;

            if (theta < 1.0f)
            {
                // Elastic adhesion + partial sliding zone
                // fn = 1 - (1 - theta)^3 = theta * (3 - 3*theta + theta^2)
                fn = theta * (3f - 3f * theta + theta * theta);
                skidIntensity = theta;
            }
            else
            {
                // Full saturation / sliding (burnout, drift, locked wheel)
                fn = 1.0f;
                skidIntensity = 1.0f;
            }

            // Coupled projections onto tire coordinate axes
            fx = maxFx * fn * (sigmaX / sigma);
            fy = maxFy * fn * (sigmaY / sigma);
        }
    }
}
