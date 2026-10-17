using UnityEngine;

namespace VALYR.Core.Interfaces
{
    /// <summary>
    /// Результат сканирования поверхности подвеской.
    /// </summary>
    public struct SurfaceHit
    {
        public bool HasHit;
        public Vector3 Point;
        public Vector3 Normal;
        public float Distance;
        public Collider Collider;
        public float FrictionMultiplier;

        public static SurfaceHit None => new()
        {
            HasHit = false,
            Point = Vector3.zero,
            Normal = Vector3.up,
            Distance = 0f,
            Collider = null,
            FrictionMultiplier = 1.0f
        };
    }

    /// <summary>
    /// Абстракция провайдера поверхности (лучи Raycast / SphereCast).
    /// </summary>
    public interface ISurfaceProvider
    {
        bool CastRay(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, out SurfaceHit hit);
    }
}
