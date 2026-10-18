using UnityEngine;

namespace VALYR.Core.Interfaces
{
    /// <summary>
    /// Surface scan hit result containing contact point, normal, distance, and surface material information.
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
    /// Abstraction for physics scene surface detection (Raycast, SphereCast, etc.).
    /// </summary>
    public interface ISurfaceProvider
    {
        /// <summary>
        /// Casts an infinitesimal ray into the scene.
        /// </summary>
        bool CastRay(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, out SurfaceHit hit);

        /// <summary>
        /// Casts a volumetric sphere into the scene.
        /// </summary>
        bool CastSphere(Vector3 origin, float radius, Vector3 direction, float maxDistance, int layerMask, out SurfaceHit hit);
    }
}
