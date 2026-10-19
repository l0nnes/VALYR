using System;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Interfaces;
using VALYR.Core.Modules;

namespace VALYR.Modules.Contact
{
    /// <summary>
    /// Geometric collision detection mode for vehicle wheels.
    /// </summary>
    public enum WheelContactMode
    {
        [Tooltip("Single vertical raycast per wheel. Ultra fast, ideal for background traffic and simple flat terrain.")]
        SingleRay = 0,

        [Tooltip("5-point contact patch (Center, Front-Bumper, Rear, Left shoulder, Right shoulder). Detects curbs, steps, and ruts ahead of the wheel with minimal performance overhead.")]
        FivePointPatch = 1,

        [Tooltip("Multi-ray radial sector array scanning the wheel circumference arc and tire width. Mathematically accurate circular profile with weighted contact blending.")]
        RadialSector = 2,

        [Tooltip("Continuous volumetric SphereCast with wheel radius along suspension travel.")]
        SphereCast = 3
    }

    /// <summary>
    /// Modular wheel contact and collision detection subsystem.
    /// Decoupled from suspension force solvers; strictly responsible for scanning scene geometry,
    /// detecting curbs/obstacles, computing contact patch normals, and determining effective damper lengths.
    /// </summary>
    [Serializable]
    public class WheelContactModule : VehicleModule, IWheelContactModule
    {
        [Header("Detection Algorithm")]
        [Tooltip("Collision detection mode used to scan road surface and obstacles.")]
        public WheelContactMode ContactMode = WheelContactMode.FivePointPatch;

        [Tooltip("Road and obstacle surface collision layers.")]
        public LayerMask SurfaceLayers = ~((1 << 2) | (1 << 3)); // Exclude IgnoreRaycast (2) and Car (3)

        [Header("Tire Dimensions")]
        [Tooltip("Nominal tread width of the tire in meters (e.g. 0.21m = 215/55R16).")]
        [Range(0.10f, 0.45f)]
        public float TireWidth = 0.21f;

        [Header("Radial Sector Configuration (For RadialSector Mode)")]
        [Tooltip("Arc angle in degrees around the lower wheel circumference (e.g. 70°).")]
        [Range(20f, 140f)]
        public float SectorAngle = 70f;

        [Tooltip("Number of radial rays along the rolling circumference arc.")]
        [Range(3, 11)]
        public int RadialResolution = 5;

        [Tooltip("Number of lateral rays across the tire width.")]
        [Range(1, 5)]
        public int WidthResolution = 3;

        [Tooltip("Effective contact width factor (0.1 to 1.0).")]
        [Range(0.2f, 1.0f)]
        public float WidthFactor = 0.90f;

        [Header("Travel Bounds")]
        [Tooltip("Maximum search extension along suspension axis from anchor in meters.")]
        [Range(0.40f, 1.20f)]
        public float MaxSearchLength = 0.60f;

        public WheelContactModule()
        {
            ModuleId = "Contact.MultiRay";
        }

        public void DetectContacts(
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
            Vector3 bodyUp = bodyRot * Vector3.up;
            Vector3 suspDown = -bodyUp;

            for (int i = 0; i < 4; i++)
            {
                ref WheelState wheel = ref state.Wheels.GetRef(i);
                float radius = chassis.WheelRadii[i];
                Vector3 mountPoint = bodyPos + bodyRot * chassis.WheelOffsets[i];

                // Compute wheel local orientation with steering angle
                float steerDeg = wheel.SteerAngle * Mathf.Rad2Deg;
                Quaternion steerRot = Quaternion.AngleAxis(steerDeg, bodyUp);
                Quaternion wheelRot = bodyRot * steerRot;
                Vector3 wheelForward = wheelRot * Vector3.forward;
                Vector3 wheelRight = wheelRot * Vector3.right;

                float maxRayDistance = MaxSearchLength + radius;

                switch (ContactMode)
                {
                    case WheelContactMode.SingleRay:
                        ScanSingleRay(surfaceProvider, mountPoint, suspDown, maxRayDistance, radius, ref wheel);
                        break;

                    case WheelContactMode.FivePointPatch:
                        ScanFivePointPatch(surfaceProvider, mountPoint, suspDown, wheelForward, wheelRight, maxRayDistance, radius, ref wheel);
                        break;

                    case WheelContactMode.RadialSector:
                        ScanRadialSector(surfaceProvider, mountPoint, suspDown, wheelForward, wheelRight, maxRayDistance, radius, ref wheel);
                        break;

                    case WheelContactMode.SphereCast:
                        ScanSphereCast(surfaceProvider, mountPoint, suspDown, maxRayDistance, radius, ref wheel);
                        break;
                }
            }
        }

        private void ScanSingleRay(
            ISurfaceProvider surfaceProvider,
            Vector3 mountPoint,
            Vector3 suspDown,
            float maxRayDistance,
            float radius,
            ref WheelState wheel)
        {
            if (surfaceProvider.CastRay(mountPoint, suspDown, maxRayDistance, SurfaceLayers, out SurfaceHit hit))
            {
                wheel.HasContact = true;
                wheel.ContactPoint = hit.Point;
                wheel.ContactNormal = hit.Normal;
                wheel.SuspensionLength = hit.Distance - radius;
                wheel.SurfaceVelocity = GetColliderVelocity(hit.Collider, hit.Point);
            }
            else
            {
                SetAirborne(mountPoint, suspDown, maxRayDistance, radius, ref wheel);
            }
        }

        private void ScanSphereCast(
            ISurfaceProvider surfaceProvider,
            Vector3 mountPoint,
            Vector3 suspDown,
            float maxRayDistance,
            float radius,
            ref WheelState wheel)
        {
            // Reduced sphere radius (0.85x) allows the volume to detect obstacles without getting stuck on side walls
            float sphereRadius = radius * 0.85f;
            float castDistance = MaxSearchLength;

            if (surfaceProvider.CastSphere(mountPoint, sphereRadius, suspDown, castDistance, SurfaceLayers, out SurfaceHit hit))
            {
                wheel.HasContact = true;
                wheel.ContactPoint = hit.Point;
                wheel.ContactNormal = hit.Normal;
                wheel.SuspensionLength = hit.Distance + (radius - sphereRadius);
                wheel.SurfaceVelocity = GetColliderVelocity(hit.Collider, hit.Point);
            }
            else
            {
                SetAirborne(mountPoint, suspDown, maxRayDistance, radius, ref wheel);
            }
        }

        private void ScanFivePointPatch(
            ISurfaceProvider surfaceProvider,
            Vector3 mountPoint,
            Vector3 suspDown,
            Vector3 wheelForward,
            Vector3 wheelRight,
            float maxRayDistance,
            float radius,
            ref WheelState wheel)
        {
            // 5 key points: Center, Forward curb detector (+0.55R), Rear (-0.40R), Left edge, Right edge
            float halfWidth = TireWidth * 0.5f * WidthFactor;
            Vector2[] sampleOffsets = new Vector2[5]
            {
                new Vector2(0f, 0f),                 // Center
                new Vector2(radius * 0.55f, 0f),     // Forward (curb / ramp detector)
                new Vector2(-radius * 0.40f, 0f),    // Rear
                new Vector2(0f, -halfWidth),         // Left shoulder
                new Vector2(0f, halfWidth)           // Right shoulder
            };

            float minDamperLength = float.MaxValue;
            Vector3 weightedPoint = Vector3.zero;
            Vector3 weightedNormal = Vector3.zero;
            float totalWeight = 0f;
            Collider governingCollider = null;
            Vector3 governingHitPoint = Vector3.zero;

            for (int k = 0; k < 5; k++)
            {
                float x = sampleOffsets[k].x;
                float y = sampleOffsets[k].y;

                // Circular profile: vertical distance from hub to tire surface at longitudinal offset x
                float profileOffset = Mathf.Sqrt(Mathf.Max(0.0001f, radius * radius - x * x));
                Vector3 origin = mountPoint + wheelForward * x + wheelRight * y;

                if (surfaceProvider.CastRay(origin, suspDown, maxRayDistance, SurfaceLayers, out SurfaceHit hit))
                {
                    // Equivalent damper length if wheel contacts this surface point
                    float candidateLength = hit.Distance - profileOffset;

                    float depth = Mathf.Max(0.001f, maxRayDistance - hit.Distance);
                    float weight = depth * depth;

                    weightedPoint += hit.Point * weight;
                    weightedNormal += hit.Normal * weight;
                    totalWeight += weight;

                    if (candidateLength < minDamperLength)
                    {
                        minDamperLength = candidateLength;
                        governingCollider = hit.Collider;
                        governingHitPoint = hit.Point;
                    }
                }
            }

            if (totalWeight > 0.0001f)
            {
                wheel.HasContact = true;
                wheel.ContactPoint = (totalWeight > 0.1f) ? (weightedPoint / totalWeight) : governingHitPoint;
                wheel.ContactNormal = (weightedNormal.sqrMagnitude > 0.001f) ? weightedNormal.normalized : Vector3.up;
                wheel.SuspensionLength = minDamperLength;
                wheel.SurfaceVelocity = GetColliderVelocity(governingCollider, wheel.ContactPoint);
            }
            else
            {
                SetAirborne(mountPoint, suspDown, maxRayDistance, radius, ref wheel);
            }
        }

        private void ScanRadialSector(
            ISurfaceProvider surfaceProvider,
            Vector3 mountPoint,
            Vector3 suspDown,
            Vector3 wheelForward,
            Vector3 wheelRight,
            float maxRayDistance,
            float radius,
            ref WheelState wheel)
        {
            int rRes = Mathf.Max(2, RadialResolution);
            int wRes = Mathf.Max(1, WidthResolution);

            float effectiveWidth = TireWidth * WidthFactor;
            float widthStep = wRes > 1 ? effectiveWidth / (wRes - 1) : 0f;
            float widthStart = -effectiveWidth * 0.5f;

            float sectorRad = SectorAngle * Mathf.Deg2Rad;
            float angleStep = sectorRad / (rRes - 1);
            float angleStart = -sectorRad * 0.5f;

            float minDamperLength = float.MaxValue;
            Vector3 weightedPoint = Vector3.zero;
            Vector3 weightedNormal = Vector3.zero;
            float totalWeight = 0f;
            Collider governingCollider = null;
            Vector3 governingHitPoint = Vector3.zero;

            for (int w = 0; w < wRes; w++)
            {
                float y = wRes > 1 ? (widthStart + widthStep * w) : 0f;

                for (int r = 0; r < rRes; r++)
                {
                    float angle = angleStart + angleStep * r;
                    float sin = Mathf.Sin(angle);
                    float cos = Mathf.Cos(angle);

                    float x = radius * sin;
                    float profileOffset = radius * cos;

                    Vector3 origin = mountPoint + wheelForward * x + wheelRight * y;

                    if (surfaceProvider.CastRay(origin, suspDown, maxRayDistance, SurfaceLayers, out SurfaceHit hit))
                    {
                        float candidateLength = hit.Distance - profileOffset;
                        float depth = Mathf.Max(0.001f, maxRayDistance - hit.Distance);
                        float weight = depth * depth;

                        weightedPoint += hit.Point * weight;
                        weightedNormal += hit.Normal * weight;
                        totalWeight += weight;

                        if (candidateLength < minDamperLength)
                        {
                            minDamperLength = candidateLength;
                            governingCollider = hit.Collider;
                            governingHitPoint = hit.Point;
                        }
                    }
                }
            }

            if (totalWeight > 0.0001f)
            {
                wheel.HasContact = true;
                wheel.ContactPoint = (totalWeight > 0.1f) ? (weightedPoint / totalWeight) : governingHitPoint;
                wheel.ContactNormal = (weightedNormal.sqrMagnitude > 0.001f) ? weightedNormal.normalized : Vector3.up;
                wheel.SuspensionLength = minDamperLength;
                wheel.SurfaceVelocity = GetColliderVelocity(governingCollider, wheel.ContactPoint);
            }
            else
            {
                SetAirborne(mountPoint, suspDown, maxRayDistance, radius, ref wheel);
            }
        }

        private static void SetAirborne(
            Vector3 mountPoint,
            Vector3 suspDown,
            float maxRayDistance,
            float radius,
            ref WheelState wheel)
        {
            wheel.HasContact = false;
            wheel.ContactPoint = mountPoint + suspDown * maxRayDistance;
            wheel.ContactNormal = -suspDown;
            wheel.SuspensionLength = maxRayDistance - radius;
            wheel.SurfaceVelocity = Vector3.zero;
        }

        private static Vector3 GetColliderVelocity(Collider collider, Vector3 worldPoint)
        {
            if (collider != null && collider.attachedRigidbody != null)
            {
                return collider.attachedRigidbody.GetPointVelocity(worldPoint);
            }
            return Vector3.zero;
        }

        /// <summary>
        /// Scene GUI helper to visualize contact rays in the Unity Scene view.
        /// </summary>
        public void DrawGizmos(
            in VehicleState state,
            in VehicleChassisConfig chassis,
            Vector3 bodyPos,
            Quaternion bodyRot)
        {
            Vector3 bodyUp = bodyRot * Vector3.up;
            Vector3 suspDown = -bodyUp;

            for (int i = 0; i < 4; i++)
            {
                float radius = chassis.WheelRadii[i];
                Vector3 mountPoint = bodyPos + bodyRot * chassis.WheelOffsets[i];
                WheelState ws = state.Wheels[i];

                float steerDeg = ws.SteerAngle * Mathf.Rad2Deg;
                Quaternion steerRot = Quaternion.AngleAxis(steerDeg, bodyUp);
                Quaternion wheelRot = bodyRot * steerRot;
                Vector3 wheelForward = wheelRot * Vector3.forward;
                Vector3 wheelRight = wheelRot * Vector3.right;

                if (ws.HasContact)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawSphere(ws.ContactPoint, 0.035f);
                    Gizmos.DrawRay(ws.ContactPoint, ws.ContactNormal * 0.25f);
                }

                // Draw five point / radial ray footprint
                if (ContactMode == WheelContactMode.FivePointPatch)
                {
                    float halfWidth = TireWidth * 0.5f * WidthFactor;
                    Vector2[] offsets = new Vector2[5]
                    {
                        new Vector2(0f, 0f),
                        new Vector2(radius * 0.55f, 0f),
                        new Vector2(-radius * 0.40f, 0f),
                        new Vector2(0f, -halfWidth),
                        new Vector2(0f, halfWidth)
                    };

                    Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.6f);
                    for (int k = 0; k < 5; k++)
                    {
                        Vector3 origin = mountPoint + wheelForward * offsets[k].x + wheelRight * offsets[k].y;
                        Gizmos.DrawRay(origin, suspDown * (MaxSearchLength + radius));
                    }
                }
            }
        }
    }
}
