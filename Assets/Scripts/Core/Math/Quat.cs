using System;

namespace Core.Math
{
    [Serializable]
    public partial struct Quat
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public Quat(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public static readonly Quat Identity = new(0, 0, 0, 1);

        public static Quat Euler(float x, float y, float z)
        {
            var cX = (float)System.Math.Cos(x * 0.5f * MathUtil.Deg2Rad);
            var sX = (float)System.Math.Sin(x * 0.5f * MathUtil.Deg2Rad);
            var cY = (float)System.Math.Cos(y * 0.5f * MathUtil.Deg2Rad);
            var sY = (float)System.Math.Sin(y * 0.5f * MathUtil.Deg2Rad);
            var cZ = (float)System.Math.Cos(z * 0.5f * MathUtil.Deg2Rad);
            var sZ = (float)System.Math.Sin(z * 0.5f * MathUtil.Deg2Rad);

            Quat q;
            q.w = cX * cY * cZ + sX * sY * sZ;
            q.x = sX * cY * cZ - cX * sY * sZ;
            q.y = cX * sY * cZ + sX * cY * sZ;
            q.z = cX * cY * sZ - sX * sY * cZ;
            return q;
        }

        public static Quat operator *(Quat lhs, Quat rhs)
        {
            return new Quat(
                lhs.w * rhs.x + lhs.x * rhs.w + lhs.y * rhs.z - lhs.z * rhs.y,
                lhs.w * rhs.y + lhs.y * rhs.w + lhs.z * rhs.x - lhs.x * rhs.z,
                lhs.w * rhs.z + lhs.z * rhs.w + lhs.x * rhs.y - lhs.y * rhs.x,
                lhs.w * rhs.w - lhs.x * rhs.x - lhs.y * rhs.y - lhs.z * rhs.z
            );
        }

        public static Vec3 operator *(Quat rotation, Vec3 point)
        {
            var num = rotation.x * 2f;
            var num2 = rotation.y * 2f;
            var num3 = rotation.z * 2f;
            var num4 = rotation.x * num;
            var num5 = rotation.y * num2;
            var num6 = rotation.z * num3;
            var num7 = rotation.x * num2;
            var num8 = rotation.x * num3;
            var num9 = rotation.y * num3;
            var num10 = rotation.w * num;
            var num11 = rotation.w * num2;
            var num12 = rotation.w * num3;

            Vec3 result;
            result.x = (1f - (num5 + num6)) * point.x + (num7 - num12) * point.y + (num8 + num11) * point.z;
            result.y = (num7 + num12) * point.x + (1f - (num4 + num6)) * point.y + (num9 - num10) * point.z;
            result.z = (num8 - num11) * point.x + (num9 + num10) * point.y + (1f - (num4 + num5)) * point.z;
            return result;
        }
    }
}