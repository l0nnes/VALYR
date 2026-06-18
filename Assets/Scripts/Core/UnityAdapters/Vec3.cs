using UnityEngine;

// ReSharper disable once CheckNamespace
namespace Core.Math
{
    public partial struct Vec3
    {
#if UNITY_5_3_OR_NEWER
        public static implicit operator Vector3(Vec3 v)
        {
            return new Vector3(v.x, v.y, v.z);
        }

        public static implicit operator Vec3(Vector3 v)
        {
            return new Vec3(v.x, v.y, v.z);
        }
#endif
    }
}