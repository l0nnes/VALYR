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

            // Total normal load supported by all grounded wheels for dynamic mass distribution
            float totalNormalLoad = 0f;
            for (int j = 0; j < 4; j++)
            {
                if (state.Wheels[j].HasContact && state.Wheels[j].NormalLoad > 0.001f)
                {
                    totalNormalLoad += state.Wheels[j].NormalLoad;
                }
            }
            Vector3 gravity = Physics.gravity;

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
                float contactSpeed = Mathf.Sqrt(vx * vx + vy * vy);

                float effectiveMuX = Mathf.Max(tireParams.FrictionCoefficientX, 0.1f);
                float effectiveMuY = Mathf.Max(tireParams.FrictionCoefficientY, 0.1f);
                float maxFx = effectiveMuX * wheel.NormalLoad;
                float maxFy = effectiveMuY * wheel.NormalLoad;

                // Corner mass supported by this specific wheel based on normal load distribution
                float cornerMass = chassis.ChassisMass * (wheel.NormalLoad / Mathf.Max(10f, totalNormalLoad));

                // Check static stiction hold (e.g. Auto Hold, handbrake, service brake at low speed / standstill)
                float totalBrakeTorque = brakeTorque + actuation[i].HandbrakeTorque;
                bool isBraked = totalBrakeTorque > 1.0f;
                bool isDriving = driveTorque > 5.0f;
                bool isLowSpeed = contactSpeed < 1.0f;

                float fx;
                float fy;
                float nextOmega;
                float nextAngle;
                float slipRatio;
                float slipAngle;
                float skidIntensity;

                if (isBraked && isLowSpeed && !isDriving)
                {
                    // Static Stiction Solver:
                    // Calculate exact forces required to cancel surface-tangent gravity acceleration
                    // and arrest any residual contact patch velocity to zero within one timestep.
                    float fHoldX = -cornerMass * (vx / dt + Vector3.Dot(gravity, wheelForward));
                    float fHoldY = -cornerMass * (vy / dt + Vector3.Dot(gravity, wheelRight));

                    // Maximum holding capacity of the tire + brake system:
                    // Longitudinal hold is limited by tire friction AND brake caliper holding force (T_brake / R)
                    float maxBrakeForce = totalBrakeTorque / radius;
                    float maxHoldX = Mathf.Min(maxFx, maxBrakeForce);
                    float maxHoldY = maxFy;

                    float normHoldX = fHoldX / Mathf.Max(maxHoldX, 0.001f);
                    float normHoldY = fHoldY / Mathf.Max(maxHoldY, 0.001f);
                    float stictionMagnitude = Mathf.Sqrt(normHoldX * normHoldX + normHoldY * normHoldY);

                    if (stictionMagnitude <= 1.0f)
                    {
                        // Complete static stiction: rock-solid standstill, zero creep, zero skid
                        fx = fHoldX;
                        fy = fHoldY;
                        nextOmega = 0f;
                        nextAngle = wheel.RotationAngle;
                        slipRatio = 0f;
                        slipAngle = 0f;
                        skidIntensity = 0f;
                    }
                    else
                    {
                        // Saturated slide (slope exceeds friction limit or weak brakes):
                        // Scale holding demand to friction boundary
                        float scale = 1.0f / stictionMagnitude;
                        fx = fHoldX * scale;
                        fy = fHoldY * scale;
                        nextOmega = 0f;
                        nextAngle = wheel.RotationAngle;
                        float vDenom = Mathf.Max(contactSpeed, 0.5f);
                        slipRatio = -vx / vDenom;
                        slipAngle = -vy / vDenom;
                        skidIntensity = 1.0f;
                    }
                }
                else
                {
                    // Dynamic Brush Tire Model (driving, rolling, or high-speed braking/sliding)

                    // 1. Lateral slip angle and lateral force calculation
                    float vDenom = Mathf.Max(contactSpeed, 0.5f);
                    slipAngle = -vy / vDenom;

                    float sigmaY = (tireParams.LateralStiffness * slipAngle) / maxFy;
                    float rawFy = maxFy * Mathf.Clamp(sigmaY, -1f, 1f);

                    // Numerical chatter prevention at low speeds (< 1.0 m/s):
                    if (contactSpeed < 1.0f)
                    {
                        float maxLateralStoppingForce = (cornerMass * Mathf.Abs(vy)) / dt;
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
                        out nextOmega,
                        out nextAngle,
                        out slipRatio,
                        out fx);

                    // Numerical chatter prevention for longitudinal forces at low speeds when not driving
                    if (contactSpeed < 1.0f && !isDriving)
                    {
                        float maxLongStoppingForce = (cornerMass * Mathf.Abs(vx)) / dt;
                        if (Mathf.Sign(fx) != Mathf.Sign(vx) && Mathf.Abs(fx) > maxLongStoppingForce)
                        {
                            fx = -Mathf.Sign(vx) * maxLongStoppingForce;
                        }
                    }

                    // 3. Friction ellipse coupling
                    float normX = fx / Mathf.Max(maxFx, 0.001f);
                    float normY = rawFy / Mathf.Max(maxFy, 0.001f);
                    float combinedMagnitude = Mathf.Sqrt(normX * normX + normY * normY);
                    fy = rawFy;

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
