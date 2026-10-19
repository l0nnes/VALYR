using UnityEngine;
using VALYR.Core.Interfaces;

namespace VALYR.Core.Adapters
{
    /// <summary>
    /// Standard Unity physics engine surface detection provider using Physics.Raycast and Physics.SphereCast.
    /// </summary>
    public sealed class UnityPhysicsSurfaceProvider : ISurfaceProvider
    {
        public bool CastRay(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, out SurfaceHit hit)
        {
            if (Physics.Raycast(origin, direction, out RaycastHit rayHit, maxDistance, layerMask, QueryTriggerInteraction.Ignore))
            {
                hit = new SurfaceHit
                {
                    HasHit = true,
                    Point = rayHit.point,
                    Normal = rayHit.normal,
                    Distance = rayHit.distance,
                    Collider = rayHit.collider,
                    FrictionMultiplier = 1.0f
                };
                return true;
            }

            hit = SurfaceHit.None;
            return false;
        }

        public bool CastSphere(Vector3 origin, float radius, Vector3 direction, float maxDistance, int layerMask, out SurfaceHit hit)
        {
            if (Physics.SphereCast(origin, radius, direction, out RaycastHit sphereHit, maxDistance, layerMask, QueryTriggerInteraction.Ignore))
            {
                hit = new SurfaceHit
                {
                    HasHit = true,
                    Point = sphereHit.point,
                    Normal = sphereHit.normal,
                    Distance = sphereHit.distance,
                    Collider = sphereHit.collider,
                    FrictionMultiplier = 1.0f
                };
                return true;
            }

            hit = SurfaceHit.None;
            return false;
        }
    }
}
