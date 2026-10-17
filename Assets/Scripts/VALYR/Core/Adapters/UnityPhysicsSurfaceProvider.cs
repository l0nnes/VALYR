using UnityEngine;
using VALYR.Core.Interfaces;

namespace VALYR.Core.Adapters
{
    /// <summary>
    /// Стандартный провайдер поверхности на основе Physics.Raycast.
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
                    FrictionMultiplier = 1.0f // Базовое значение, в будущем расширяемо материалами поверхности
                };
                return true;
            }

            hit = SurfaceHit.None;
            return false;
        }
    }
}
