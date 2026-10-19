using System;
using UnityEngine;

namespace VALYR.Core.Math
{
    /// <summary>
    /// Wheel rotational dynamics solver.
    /// Provides explicit integration for airborne wheels and an unconditionally stable
    /// semi-implicit solver for wheels in road contact, eliminating numerical chatter
    /// and wheel oscillations at low speeds.
    /// </summary>
    public static class WheelDynamics
    {
        /// <summary>
        /// Integrates angular velocity for a free (airborne) wheel.
        /// </summary>
        public static void Integrate(
            float currentOmega,
            float currentRotationAngle,
            float driveTorque,
            float brakeTorque,
            float tireLongitudinalForce,
            float wheelRadius,
            float wheelInertia,
            float dt,
            out float nextOmega,
            out float nextRotationAngle)
        {
            if (wheelInertia < 0.01f)
                wheelInertia = 0.01f;

            float tireReactionTorque = tireLongitudinalForce * wheelRadius;
            float maxBrakeToStop = (Mathf.Abs(currentOmega) * wheelInertia) / dt;
            float effectiveBrake = Mathf.Min(brakeTorque, maxBrakeToStop) * Mathf.Sign(currentOmega);
            float bearingDrag = 2.0f * currentOmega;

            float netTorque = driveTorque - tireReactionTorque - effectiveBrake - bearingDrag;
            float deltaOmega = (netTorque / wheelInertia) * dt;
            nextOmega = currentOmega + deltaOmega;
            nextRotationAngle = Mathf.Repeat(currentRotationAngle + nextOmega * dt, Mathf.PI * 2f);
        }

        /// <summary>
        /// Unconditionally stable semi-implicit integrator for a wheel in contact with road surface.
        /// Solves stiff differential equations at near-zero speeds without numerical blowup.
        /// </summary>
        public static void IntegrateContact(
            float currentOmega,
            float currentRotationAngle,
            float driveTorque,
            float brakeTorque,
            float forwardSpeed,
            float wheelRadius,
            float wheelInertia,
            float longitudinalStiffness,
            float maxFx,
            float dt,
            out float nextOmega,
            out float nextRotationAngle,
            out float longitudinalSlip,
            out float appliedFx)
        {
            if (wheelInertia < 0.01f)
                wheelInertia = 0.01f;

            float r = wheelRadius;
            float vDenom = Mathf.Max(Mathf.Abs(forwardSpeed), 0.5f);

            // 1. Unconstrained rotation under drive torque, bearing drag, and tire adhesion (ignoring brakes)
            float bearingDrag = 1.0f * currentOmega;
            float netDriveTorque = driveTorque - bearingDrag;

            // Semi-implicit stiffness coefficient: K = (Cx * R^2 * dt) / vDenom
            float kImplicit = (longitudinalStiffness * r * r * dt) / vDenom;
            float effectiveInertia = wheelInertia + kImplicit;

            // Free angular velocity of the wheel before applying disc/parking brake torque
            float omegaFree = (wheelInertia * currentOmega + netDriveTorque * dt + (longitudinalStiffness * r * dt / vDenom) * forwardSpeed) / effectiveInertia;

            // 2. Exact brake torque resolution: if brake torque exceeds required stopping torque, lock wheel (omega = 0)
            float torqueToStop = (Mathf.Abs(omegaFree) * effectiveInertia) / dt;
            float targetOmega;

            if (brakeTorque >= torqueToStop && brakeTorque > 0.001f)
            {
                // Brake fully holds / locks the wheel
                targetOmega = 0f;
            }
            else
            {
                // Brake decelerates the wheel
                targetOmega = omegaFree - Mathf.Sign(omegaFree) * (brakeTorque * dt / effectiveInertia);
            }

            // Next timestep longitudinal slip ratio
            longitudinalSlip = (targetOmega * r - forwardSpeed) / vDenom;

            // Tire traction force
            float rawFx = longitudinalStiffness * longitudinalSlip;

            if (Mathf.Abs(rawFx) > maxFx && maxFx > 0.001f)
            {
                // Wheel slip exceeds friction limit (burnout or slide)
                appliedFx = Mathf.Sign(rawFx) * maxFx;

                float netTorque = driveTorque - appliedFx * r - bearingDrag;
                if (targetOmega == 0f && brakeTorque >= Mathf.Abs(netTorque))
                {
                    nextOmega = 0f;
                }
                else
                {
                    float freeAccel = netTorque / wheelInertia;
                    float stopBrakeTorque = (Mathf.Abs(currentOmega + freeAccel * dt) * wheelInertia) / dt;
                    float effectiveBrake = Mathf.Min(brakeTorque, stopBrakeTorque) * Mathf.Sign(currentOmega + freeAccel * dt);

                    nextOmega = currentOmega + ((netTorque - effectiveBrake) / wheelInertia) * dt;
                }
                longitudinalSlip = (nextOmega * r - forwardSpeed) / vDenom;
            }
            else
            {
                // Elastic adhesion (no slip rupture)
                appliedFx = rawFx;
                nextOmega = targetOmega;
            }

            nextRotationAngle = Mathf.Repeat(currentRotationAngle + nextOmega * dt, Mathf.PI * 2f);
        }
    }
}
