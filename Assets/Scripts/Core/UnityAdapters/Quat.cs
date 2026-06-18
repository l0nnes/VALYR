using UnityEngine;

// ReSharper disable once CheckNamespace
namespace Core.Math
{
    public partial struct Quat
    {
#if UNITY_5_3_OR_NEWER
        public static implicit operator Quaternion(Quat q)
        {
            return new Quaternion(q.x, q.y, q.z, q.w);
        }

        public static implicit operator Quat(Quaternion q)
        {
            return new Quat(q.x, q.y, q.z, q.w);
        }
#endif
    }
}