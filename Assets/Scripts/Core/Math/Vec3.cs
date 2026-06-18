using System;

namespace Core.Math
{
    [Serializable]
    public partial struct Vec3
    {
        public float x;
        public float y;
        public float z;

        public Vec3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static readonly Vec3 Zero = new(0, 0, 0);
        public static readonly Vec3 One = new(1, 1, 1);
        public static readonly Vec3 Up = new(0, 1, 0);
        public static readonly Vec3 Right = new(1, 0, 0);
        public static readonly Vec3 Forward = new(0, 0, 1);

        public static Vec3 operator +(Vec3 a, Vec3 b)
        {
            return new Vec3(a.x + b.x, a.y + b.y, a.z + b.z);
        }

        public static Vec3 operator -(Vec3 a, Vec3 b)
        {
            return new Vec3(a.x - b.x, a.y - b.y, a.z - b.z);
        }

        public static Vec3 operator -(Vec3 a)
        {
            return new Vec3(-a.x, -a.y, -a.z);
        }

        public static Vec3 operator *(Vec3 a, float d)
        {
            return new Vec3(a.x * d, a.y * d, a.z * d);
        }

        public static Vec3 operator *(float d, Vec3 a)
        {
            return new Vec3(a.x * d, a.y * d, a.z * d);
        }

        public static Vec3 operator /(Vec3 a, float d)
        {
            return new Vec3(a.x / d, a.y / d, a.z / d);
        }

        public float Magnitude()
        {
            return (float)System.Math.Sqrt(x * x + y * y + z * z);
        }

        public float SqrMagnitude()
        {
            return x * x + y * y + z * z;
        }

        public Vec3 Normalize()
        {
            var m = Magnitude();
            return m > 1E-05f ? this / m : Zero;
        }

        public static float Dot(Vec3 lhs, Vec3 rhs)
        {
            return lhs.x * rhs.x + lhs.y * rhs.y + lhs.z * rhs.z;
        }

        public static Vec3 Cross(Vec3 lhs, Vec3 rhs)
        {
            return new Vec3(
                lhs.y * rhs.z - lhs.z * rhs.y,
                lhs.z * rhs.x - lhs.x * rhs.z,
                lhs.x * rhs.y - lhs.y * rhs.x);
        }

        public static Vec3 ProjectOnPlane(Vec3 vector, Vec3 planeNormal)
        {
            var num = Dot(planeNormal, planeNormal);
            if (num < MathUtil.Epsilon) return vector;
            var num2 = Dot(vector, planeNormal);
            return new Vec3(vector.x - planeNormal.x * num2 / num, vector.y - planeNormal.y * num2 / num,
                vector.z - planeNormal.z * num2 / num);
        }

        public override string ToString()
        {
            return "(" + x + ", " + y + ", " + z + ")";
        }
    }
}