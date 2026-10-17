using System;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Interfaces;
using VALYR.Core.Modules;

namespace VALYR.Modules.Suspension
{
    /// <summary>
    /// Physical suspension axle configuration (front or rear).
    /// </summary>
    [Serializable]
    public struct SuspensionAxleSettings
    {
        [Tooltip("Unloaded rest length of the spring in meters (e.g. 0.35m).")]
        [Range(0.1f, 0.8f)]
        public float RestLength;

        [Tooltip("Maximum allowable suspension travel / compression distance in meters (e.g. 0.22m for road car, 0.35m for SUV).")]
        [Range(0.05f, 0.6f)]
        public float MaxTravel;

        [Tooltip("Linear spring stiffness in Newtons per meter (N/m). Road sedan: 20000 - 28000 N/m, Sports car: 40000 - 65000 N/m.")]
        [Min(1000f)]
        public float SpringStiffness;

        [Tooltip("Shock absorber damping rate during compression / bump (N*s/m). Controls body dive and ride harshness.")]
        [Min(100f)]
        public float DamperCompressionRate;

        [Tooltip("Shock absorber damping rate during rebound / extension (N*s/m). Typically 1.4 - 1.8x higher than compression to control body float.")]
        [Min(100f)]
        public float DamperReboundRate;

        [Tooltip("Relative compression travel threshold where the progressive polyurethane bump stop engages (0.75 - 0.85).")]
        [Range(0.5f, 0.95f)]
        public float BumpStopThresholdRatio;

        [Tooltip("Progressive quadratic stiffness of the bump stop in N/m^2. Prevents suspension bottoming out hard.")]
        [Min(1000f)]
        public float BumpStopStiffness;

        public static SuspensionAxleSettings DefaultFront => new()
        {
            RestLength = 0.35f,
            MaxTravel = 0.22f,
            SpringStiffness = 24000f,
            DamperCompressionRate = 2200f,
            DamperReboundRate = 3400f,
            BumpStopThresholdRatio = 0.80f,
            BumpStopStiffness = 140000f
        };

        public static SuspensionAxleSettings DefaultRear => new()
        {
            RestLength = 0.35f,
            MaxTravel = 0.22f,
            SpringStiffness = 19000f,
            DamperCompressionRate = 1800f,
            DamperReboundRate = 2800f,
            BumpStopThresholdRatio = 0.80f,
            BumpStopStiffness = 140000f
        };
    }

    /// <summary>
    /// Independent raycast suspension module with separate front and rear axle tuning,
    /// asymmetric bump/rebound damping, and progressive bump stop bottoming-out protection.
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
                float maxRayDistance = settings.RestLength + radius;

                bool hasHit = surfaceProvider.CastRay(
                    mountPoint,
                    suspDown,
                    maxRayDistance,
                    SurfaceLayers,
                    out SurfaceHit hit);

                ref WheelState wheel = ref state.Wheels.GetRef(i);

                if (hasHit)
                {
                    float currentLength = hit.Distance - radius;
                    float compression = Mathf.Clamp(settings.RestLength - currentLength, 0f, settings.MaxTravel);

                    // Suspension compression velocity (+ compression as body moves toward ground, - rebound)
                    Vector3 mountVelocity = body.GetPointVelocity(mountPoint);
                    float suspVelocity = Vector3.Dot(mountVelocity, suspDown);

                    // 1. Hooke's linear spring force
                    float fSpring = settings.SpringStiffness * compression;

                    // 2. Shock absorber damper force with asymmetric bump vs rebound
                    float damperCoeff = suspVelocity >= 0f
                        ? settings.DamperCompressionRate
                        : settings.DamperReboundRate;
                    float fDamper = damperCoeff * suspVelocity;

                    // 3. Progressive polyurethane bump stop
                    float bumpThreshold = settings.MaxTravel * settings.BumpStopThresholdRatio;
                    float fBump = 0f;
                    if (compression > bumpThreshold)
                    {
                        float excess = compression - bumpThreshold;
                        fBump = settings.BumpStopStiffness * excess * excess;
                    }

                    // Resultant suspension normal load (suspension cannot pull vehicle downward)
                    float totalForce = Mathf.Max(0f, fSpring + fDamper + fBump);

                    // Apply suspension upward force at the mount point on the chassis body
                    Vector3 forceVector = -suspDown * totalForce;
                    body.AddForceAtPosition(forceVector, mountPoint);

                    // Update wheel contact state
                    wheel.HasContact = true;
                    wheel.ContactPoint = hit.Point;
                    wheel.ContactNormal = hit.Normal;
                    wheel.SuspensionCompression = compression;
                    wheel.SuspensionVelocity = suspVelocity;
                    wheel.NormalLoad = totalForce;
                }
                else
                {
                    // Wheel airborne
                    wheel.HasContact = false;
                    wheel.ContactPoint = mountPoint + suspDown * maxRayDistance;
                    wheel.ContactNormal = -suspDown;
                    wheel.SuspensionCompression = 0f;
                    wheel.SuspensionVelocity = 0f;
                    wheel.NormalLoad = 0f;
                }
            }
        }
    }
}
