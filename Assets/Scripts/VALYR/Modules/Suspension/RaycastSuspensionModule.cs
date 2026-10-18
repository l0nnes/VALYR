using System;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Interfaces;
using VALYR.Core.Modules;

namespace VALYR.Modules.Suspension
{
    /// <summary>
    /// Physical suspension axle configuration (front or rear).
    /// Parameterized by physical damper lengths from the anchor point down to the wheel hub center.
    /// </summary>
    [Serializable]
    public struct SuspensionAxleSettings
    {
        [Header("Damper Length Limits (Anchor to Wheel Hub Center)")]
        [Tooltip("Target ride height length of the damper in meters (nominal equilibrium position where spring is at rest).")]
        [Range(0.20f, 1.0f)]
        public float TargetLength;

        [Tooltip("Minimum allowable damper length in meters (hard bump stop limit). The wheel physically CANNOT rise closer to the anchor than this. Must be greater than or equal to wheel radius to prevent arch clipping.")]
        [Range(0.15f, 0.80f)]
        public float MinLength;

        [Tooltip("Maximum allowable damper length in meters when wheel is airborne / on droop extension.")]
        [Range(0.25f, 1.20f)]
        public float MaxLength;

        [Header("Spring & Damping Dynamics")]
        [Tooltip("Linear spring stiffness in Newtons per meter (N/m). Sedan: 24000 - 32000 N/m, Sports car: 35000 - 55000 N/m.")]
        [Min(1000f)]
        public float SpringStiffness;

        [Tooltip("Shock absorber damping rate during compression / bump (N*s/m). Controls body dive and landing impact absorption.")]
        [Min(100f)]
        public float DamperCompressionRate;

        [Tooltip("Shock absorber damping rate during rebound / extension (N*s/m). Typically 1.3 - 1.6x compression rate to control body oscillation.")]
        [Min(100f)]
        public float DamperReboundRate;

        [Header("Progressive Bump Stop Buffer")]
        [Tooltip("Buffer distance above MinLength where the progressive polyurethane bump stop engages (meters, e.g. 0.04m = 4cm).")]
        [Range(0.01f, 0.15f)]
        public float BumpStopBuffer;

        [Tooltip("Progressive quadratic bump stop stiffness in N/m^2. Provides an unyielding deceleration cushion before reaching MinLength.")]
        [Min(1000f)]
        public float BumpStopStiffness;

        public static SuspensionAxleSettings DefaultFront => new()
        {
            TargetLength = 0.44f,
            MinLength = 0.35f,
            MaxLength = 0.58f,
            SpringStiffness = 28000f,
            DamperCompressionRate = 3000f,
            DamperReboundRate = 4200f,
            BumpStopBuffer = 0.04f,
            BumpStopStiffness = 350000f
        };

        public static SuspensionAxleSettings DefaultRear => new()
        {
            TargetLength = 0.44f,
            MinLength = 0.35f,
            MaxLength = 0.58f,
            SpringStiffness = 24000f,
            DamperCompressionRate = 2600f,
            DamperReboundRate = 3800f,
            BumpStopBuffer = 0.04f,
            BumpStopStiffness = 350000f
        };
    }

    /// <summary>
    /// Independent raycast suspension module with separate front and rear axle tuning,
    /// hard MinLength arch protection, progressive bump stop buffer, and asymmetric bump/rebound damping.
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
                    // Current distance from mount point (anchor) to wheel hub center
                    float currentLength = hit.Distance - radius;

                    // Strictly enforce damper travel bounds [MinLength, MaxLength]
                    currentLength = Mathf.Clamp(currentLength, settings.MinLength, settings.MaxLength);

                    // Compression relative to TargetLength (positive = compressed shorter than TargetLength)
                    float compression = settings.TargetLength - currentLength;

                    // Suspension compression velocity (+ compression as body moves toward ground, - rebound)
                    Vector3 mountVelocity = body.GetPointVelocity(mountPoint);
                    float suspVelocity = Vector3.Dot(mountVelocity, suspDown);

                    // 1. Hooke's linear spring force around TargetLength
                    float fSpring = settings.SpringStiffness * compression;

                    // 2. Shock absorber damper force with asymmetric bump vs rebound
                    float damperCoeff = suspVelocity >= 0f
                        ? settings.DamperCompressionRate
                        : settings.DamperReboundRate;
                    float fDamper = damperCoeff * suspVelocity;

                    // 3. Progressive bump stop buffer approaching MinLength
                    float bumpThresholdLength = settings.MinLength + settings.BumpStopBuffer;
                    float fBump = 0f;
                    if (currentLength < bumpThresholdLength)
                    {
                        float excess = bumpThresholdLength - currentLength;
                        fBump = settings.BumpStopStiffness * excess * excess;

                        // Hard bottoming stop: if compressed to MinLength, arrest any remaining downward velocity
                        if (currentLength <= settings.MinLength + 0.001f && suspVelocity > 0f)
                        {
                            float quarterMass = chassis.ChassisMass * 0.25f;
                            fBump += (quarterMass * suspVelocity) / dt;
                        }
                    }

                    // Resultant suspension normal load (suspension cannot pull vehicle downward into the ground)
                    float totalForce = Mathf.Max(0f, fSpring + fDamper + fBump);

                    // Apply suspension upward force at mount point on chassis
                    Vector3 forceVector = -suspDown * totalForce;
                    body.AddForceAtPosition(forceVector, mountPoint);

                    // Update wheel state
                    wheel.HasContact = true;
                    wheel.ContactPoint = hit.Point;
                    wheel.ContactNormal = hit.Normal;
                    wheel.SuspensionLength = currentLength;
                    wheel.SuspensionCompression = compression;
                    wheel.SuspensionVelocity = suspVelocity;
                    wheel.NormalLoad = totalForce;
                }
                else
                {
                    // Wheel airborne: fully extended to MaxLength
                    wheel.HasContact = false;
                    wheel.ContactPoint = mountPoint + suspDown * maxRayDistance;
                    wheel.ContactNormal = -suspDown;
                    wheel.SuspensionLength = settings.MaxLength;
                    wheel.SuspensionCompression = settings.TargetLength - settings.MaxLength;
                    wheel.SuspensionVelocity = 0f;
                    wheel.NormalLoad = 0f;
                }
            }
        }
    }
}
