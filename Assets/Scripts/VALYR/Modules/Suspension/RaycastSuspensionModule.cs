using System;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Interfaces;
using VALYR.Core.Modules;

namespace VALYR.Modules.Suspension
{
    /// <summary>
    /// Physical suspension axle configuration (front or rear).
    /// Parameterized by physical damper lengths from the anchor point down to the wheel hub center:
    /// - MinLength: hard bump stop limit (cannot rise closer to anchor than this, protects wheel arch).
    /// - TargetLength: nominal ride height equilibrium where spring balances vehicle weight.
    /// - MaxLength: maximum extended length when airborne (droop limit).
    /// </summary>
    [Serializable]
    public struct SuspensionAxleSettings
    {
        [Header("Damper Lengths (Anchor down to Wheel Hub Center)")]
        [Tooltip("Target ride height length of the damper in meters. The spring balances the vehicle weight at this exact length.")]
        [Range(0.20f, 1.0f)]
        public float TargetLength;

        [Tooltip("Minimum allowable damper length in meters (hard bump stop limit). The wheel physically CANNOT rise closer to the anchor than this. Must be greater than or equal to wheel radius to prevent arch clipping.")]
        [Range(0.15f, 0.80f)]
        public float MinLength;

        [Tooltip("Maximum allowable damper length in meters when wheel is airborne / on droop extension.")]
        [Range(0.25f, 1.20f)]
        public float MaxLength;

        [Header("Spring & Damping Dynamics")]
        [Tooltip("Linear spring stiffness in Newtons per meter (N/m). Controls resistance to body roll, pitch, and bumps.")]
        [Min(1000f)]
        public float SpringStiffness;

        [Tooltip("Shock absorber damping rate during compression / bump (N*s/m). Controls body dive and landing impact absorption.")]
        [Min(100f)]
        public float DamperCompressionRate;

        [Tooltip("Shock absorber damping rate during rebound / extension (N*s/m). Typically 1.3 - 1.6x compression rate to control body oscillation.")]
        [Min(100f)]
        public float DamperReboundRate;

        [Header("Progressive Bump Stop Buffer")]
        [Tooltip("Buffer distance above MinLength where the progressive polyurethane bump stop engages (meters, e.g. 0.03m = 3cm).")]
        [Range(0.01f, 0.15f)]
        public float BumpStopBuffer;

        [Tooltip("Progressive bump stop stiffness in N/m. Provides progressive deceleration cushion before reaching MinLength.")]
        [Min(1000f)]
        public float BumpStopStiffness;

        public static SuspensionAxleSettings DefaultFront => new()
        {
            TargetLength = 0.38f,
            MinLength = 0.33f,
            MaxLength = 0.55f,
            SpringStiffness = 32000f,
            DamperCompressionRate = 3200f,
            DamperReboundRate = 4500f,
            BumpStopBuffer = 0.03f,
            BumpStopStiffness = 150000f
        };

        public static SuspensionAxleSettings DefaultRear => new()
        {
            TargetLength = 0.38f,
            MinLength = 0.33f,
            MaxLength = 0.55f,
            SpringStiffness = 28000f,
            DamperCompressionRate = 2800f,
            DamperReboundRate = 4000f,
            BumpStopBuffer = 0.03f,
            BumpStopStiffness = 150000f
        };
    }

    /// <summary>
    /// Independent raycast suspension module with separate front and rear axle tuning,
    /// equilibrium-centered spring forces, hard MinLength arch protection, and progressive bump stop buffers.
    /// </summary>
    [Serializable]
    public class RaycastSuspensionModule : VehicleModule, ISuspensionModule
    {
        [Header("Axle Settings")]
        [Tooltip("Suspension settings for the front axle (FL, FR).")]
        public SuspensionAxleSettings FrontAxle = SuspensionAxleSettings.DefaultFront;

        [Tooltip("Suspension settings for the rear axle (RL, RR).")]
        public SuspensionAxleSettings RearAxle = SuspensionAxleSettings.DefaultRear;

        [Header("Surface Detection")]
        [Tooltip("Layer mask specifying ground colliders that the suspension raycast detects.")]
        public LayerMask SurfaceLayers = ~0;

        public RaycastSuspensionModule()
        {
            ModuleId = "Suspension.Raycast";
        }

        public void SolveSuspension(
            ref VehicleState state,
            IPhysicsBody body,
            ISurfaceProvider surfaceProvider,
            in VehicleChassisConfig chassis,
            float dt)
        {
            if (!IsEnabled)
                return;

            Vector3 bodyPos = body.Position;
            Quaternion bodyRot = body.Rotation;
            Vector3 suspDown = -(bodyRot * Vector3.up);

            for (int i = 0; i < 4; i++)
            {
                bool isFront = (i == 0 || i == 1);
                ref readonly SuspensionAxleSettings settings = ref (isFront ? ref FrontAxle : ref RearAxle);

                float radius = chassis.WheelRadii[i];
                Vector3 mountPoint = bodyPos + bodyRot * chassis.WheelOffsets[i];
                float maxRayDistance = settings.MaxLength + radius;

                bool hasHit = surfaceProvider.CastRay(
                    mountPoint,
                    suspDown,
                    maxRayDistance,
                    SurfaceLayers,
                    out SurfaceHit hit);

                ref WheelState wheel = ref state.Wheels.GetRef(i);

                if (hasHit)
                {
                    // Actual physical distance from mount point (anchor) to wheel hub on ground
                    float currentLength = hit.Distance - radius;

                    // Hard arch protection: minimum damper length cannot be less than wheel radius
                    float safeMinLength = Mathf.Max(settings.MinLength, radius);
                    float safeTargetLength = Mathf.Max(settings.TargetLength, safeMinLength + 0.01f);
                    float safeMaxLength = Mathf.Max(settings.MaxLength, safeTargetLength + 0.02f);

                    // Compression relative to TargetLength (positive = compressed shorter than TargetLength)
                    float compression = safeTargetLength - currentLength;

                    // Nominal static weight per wheel: spring naturally balances vehicle weight at TargetLength
                    // Front axle carries ~54% of curb mass, rear axle carries ~46%
                    float frontRatio = 0.54f;
                    float cornerMass = chassis.ChassisMass * (isFront ? (frontRatio * 0.5f) : ((1f - frontRatio) * 0.5f));
                    float fStatic = cornerMass * 9.81f;

                    // 1. Hooke's Spring Force centered at TargetLength (preload balances gravity at TargetLength)
                    float fSpring = fStatic + settings.SpringStiffness * compression;

                    // Measure suspension velocity along damper axis (+ is compressing downward, - is extending/rebound)
                    Vector3 mountVelocity = body.GetPointVelocity(mountPoint);
                    float suspVelocity = Vector3.Dot(mountVelocity, suspDown);

                    // 2. Progressive Bump Stop Buffer approaching safeMinLength
                    float bumpThresholdLength = safeMinLength + settings.BumpStopBuffer;
                    float fBump = 0f;
                    float extraBumpDamping = 0f;

                    if (currentLength < bumpThresholdLength)
                    {
                        float penetration = bumpThresholdLength - currentLength;
                        float bufferNorm = Mathf.Clamp01(penetration / Mathf.Max(0.001f, settings.BumpStopBuffer));

                        // Progressive polyurethane cushion
                        fBump = settings.BumpStopStiffness * penetration * (1f + 3f * bufferNorm);

                        // Polyurethane bump stops dissipate substantial kinetic energy
                        extraBumpDamping = settings.DamperCompressionRate * 2.0f * bufferNorm;

                        // Hard bottoming stop: if compressed to safeMinLength while moving downward, arrest downward velocity
                        if (currentLength <= safeMinLength && suspVelocity > 0f)
                        {
                            fBump += (cornerMass * suspVelocity) / dt;
                        }
                    }

                    // 3. Shock Absorber Damping (asymmetric bump vs rebound)
                    float baseDamperCoeff = suspVelocity >= 0f
                        ? (settings.DamperCompressionRate + extraBumpDamping)
                        : settings.DamperReboundRate;
                    float fDamper = baseDamperCoeff * suspVelocity;

                    // Resultant suspension normal load (suspension cannot pull vehicle downward into the ground)
                    float totalForce = Mathf.Max(0f, fSpring + fDamper + fBump);

                    // Apply suspension upward force at mount point
                    Vector3 forceVector = -suspDown * totalForce;
                    body.AddForceAtPosition(forceVector, mountPoint);

                    // Physical clamped length for wheel hub (guaranteed within [safeMinLength, safeMaxLength])
                    float effectiveLength = Mathf.Clamp(currentLength, safeMinLength, safeMaxLength);

                    // Update wheel state
                    wheel.HasContact = true;
                    wheel.ContactPoint = hit.Point;
                    wheel.ContactNormal = hit.Normal;
                    wheel.SuspensionLength = effectiveLength;
                    wheel.SuspensionCompression = compression;
                    wheel.SuspensionVelocity = suspVelocity;
                    wheel.NormalLoad = totalForce;
                }
                else
                {
                    float safeMaxLength = Mathf.Max(settings.MaxLength, settings.TargetLength + 0.02f);

                    // Wheel airborne: fully extended to MaxLength
                    wheel.HasContact = false;
                    wheel.ContactPoint = mountPoint + suspDown * maxRayDistance;
                    wheel.ContactNormal = -suspDown;
                    wheel.SuspensionLength = safeMaxLength;
                    wheel.SuspensionCompression = settings.TargetLength - safeMaxLength;
                    wheel.SuspensionVelocity = 0f;
                    wheel.NormalLoad = 0f;
                }
            }
        }
    }
}
