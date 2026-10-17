using System;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Interfaces;
using VALYR.Core.Math;
using VALYR.Core.Modules;

namespace VALYR.Modules.Tires
{
    /// <summary>
    /// Tire dynamics module based on the analytical Brush Tire Model (Fiala / Pacejka hybrid).
    /// Computes non-linear combined slip ellipse, eliminates low-speed chatter/drift,
    /// and solves rotational wheel dynamics differential equations (WheelDynamics).
    /// </summary>
    [Serializable]
    public class BrushTireModule : VehicleModule, ITireModule
    {
        [Header("Front Axle Tires")]
        [Tooltip("Physical grip and stiffness properties of front tires.")]
        public BrushTireParameters FrontTires = BrushTireParameters.Default;

        [Header("Rear Axle Tires")]
        [Tooltip("Physical grip and stiffness properties of rear tires.")]
        public BrushTireParameters RearTires = BrushTireParameters.Default;

        [Header("Wheel Rotational Inertia")]
        [Tooltip("Rotational moment of inertia of each wheel assembly and brake disc in kg*m^2 (typical: 0.9 - 1.5 kg*m^2 for civilian cars, 2.0+ for heavy SUVs).")]
        [Min(0.05f)]
        public float WheelInertia = 1.1f;

        public BrushTireModule()
        {
            ModuleId = "Tires.Brush";
        }

        public void SolveTires(
            ref VehicleState state,
            in WheelData4<WheelActuation> actuation,
            IPhysicsBody body,
            in VehicleChassisConfig chassis,
            float dt)
        {
            if (!IsEnabled)
                return;

            Quaternion bodyRot = body.Rotation;
            Vector3 bodyUp = bodyRot * Vector3.up;
            Vector3 bodyForward = bodyRot * Vector3.forward;

            for (int i = 0; i < 4; i++)
            {
                bool isFront = (i == 0 || i == 1);
                ref readonly BrushTireParameters tireParams = ref (isFront ? ref FrontTires : ref RearTires);

                float radius = chassis.WheelRadii[i];
                ref WheelState wheel = ref state.Wheels.GetRef(i);
                float driveTorque = actuation[i].GetEffectiveDriveTorque();
                float brakeTorque = actuation[i].GetEffectiveBrakeTorque();

                if (!wheel.HasContact || wheel.NormalLoad <= 0.001f)
                {
                    // Wheel airborne: free rotational integration
                    WheelDynamics.Integrate(
                        wheel.AngularVelocity,
                        wheel.RotationAngle,
                        driveTorque,
                        brakeTorque,
                        0f,
                        radius,
                        WheelInertia,
                        dt,
                        out float freeOmega,
                        out float freeAngle);

                    wheel.AngularVelocity = freeOmega;
                    wheel.RotationAngle = freeAngle;
                    wheel.SlipRatio = 0f;
                    wheel.SlipAngle = 0f;
                    wheel.TireForce = Vector3.zero;
                    wheel.SkidIntensity = 0f;
                    continue;
                }

                // Wheel in road contact: compute local contact patch basis accounting for steer angle
                Vector3 steeredDir = Quaternion.AngleAxis(Mathf.Rad2Deg * wheel.SteerAngle, bodyUp) * bodyForward;
                Vector3 wheelRight = Vector3.Cross(wheel.ContactNormal, steeredDir).normalized;
                Vector3 wheelForward = Vector3.Cross(wheelRight, wheel.ContactNormal).normalized;

                // Linear velocity of contact patch on ground
                Vector3 contactVelocity = body.GetPointVelocity(wheel.ContactPoint);
                float vx = Vector3.Dot(contactVelocity, wheelForward);
                float vy = Vector3.Dot(contactVelocity, wheelRight);
                float contactLinearSpeed = contactVelocity.magnitude;

                // 1. Lateral slip angle and lateral force calculation
                float contactSpeed = Mathf.Sqrt(vx * vx + vy * vy);
                float vDenom = Mathf.Max(contactSpeed, 0.5f);
                float slipAngle = -vy / vDenom;

                float effectiveMuX = Mathf.Max(tireParams.FrictionCoefficientX, 0.1f);
                float effectiveMuY = Mathf.Max(tireParams.FrictionCoefficientY, 0.1f);
                float maxFx = effectiveMuX * wheel.NormalLoad;
                float maxFy = effectiveMuY * wheel.NormalLoad;

                float sigmaY = (tireParams.LateralStiffness * slipAngle) / maxFy;
                float rawFy = maxFy * Mathf.Clamp(sigmaY, -1f, 1f);

                // Numerical chatter prevention at low speeds (< 1.0 m/s):
                // Lateral force must not exceed the force required to arrest lateral motion in one timestep.
                if (contactSpeed < 1.0f)
                {
                    float quarterMass = chassis.ChassisMass * 0.25f;
                    float maxLateralStoppingForce = (quarterMass * Mathf.Abs(vy)) / dt;
                    if (Mathf.Abs(rawFy) > maxLateralStoppingForce)
                    {
                        rawFy = Mathf.Sign(rawFy) * maxLateralStoppingForce;
                    }
                }

                // 2. Unconditionally stable semi-implicit longitudinal rotational solver
                WheelDynamics.IntegrateContact(
                    wheel.AngularVelocity,
                    wheel.RotationAngle,
                    driveTorque,
                    brakeTorque,
                    vx,
                    radius,
                    WheelInertia,
                    tireParams.LongitudinalStiffness,
                    maxFx,
                    dt,
                    out float nextOmega,
                    out float nextAngle,
                    out float slipRatio,
                    out float fx);

                // 3. Friction ellipse coupling
                float normX = fx / Mathf.Max(maxFx, 0.001f);
                float normY = rawFy / Mathf.Max(maxFy, 0.001f);
                float combinedMagnitude = Mathf.Sqrt(normX * normX + normY * normY);
                float fy = rawFy;
                float skidIntensity = 0f;

                if (combinedMagnitude > 1.0f)
                {
                    float scale = 1.0f / combinedMagnitude;
                    fx *= scale;
                    fy *= scale;
                    skidIntensity = 1.0f;
                }
                else
                {
                    skidIntensity = combinedMagnitude;
                }

                // Apply resultant 3D tire forces to vehicle chassis
                Vector3 worldTireForce = wheelForward * fx + wheelRight * fy;
                body.AddForceAtPosition(worldTireForce, wheel.ContactPoint);

                // Update wheel state
                wheel.AngularVelocity = nextOmega;
                wheel.RotationAngle = nextAngle;
                wheel.SlipRatio = slipRatio;
                wheel.SlipAngle = slipAngle;
                wheel.TireForce = worldTireForce;
                wheel.SkidIntensity = skidIntensity;
            }
        }
    }
}
