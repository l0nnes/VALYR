namespace Core.Math
{
    public static class MathUtil
    {
        public const float Epsilon = 1E-05f;
        public const float PI = 3.14159274f;
        public const float Deg2Rad = 0.0174532924f;
        public const float Rad2Deg = 57.29578f;

        public static float Exp(float power)
        {
            return (float)System.Math.Exp(power);
        }

        public static float Pow(float baseValue, float exponent)
        {
            return (float)System.Math.Pow(baseValue, exponent);
        }

        public static float Clamp(float value, float min, float max)
        {
            return value < min ? min : value > max ? max : value;
        }

        public static float Clamp01(float value)
        {
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }

        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Clamp01(t);
        }

        public static float InverseLerp(float a, float b, float value)
        {
            return Abs(b - a) < Epsilon ? 0f : Clamp01((value - a) / (b - a));
        }

        public static float Abs(float f)
        {
            return f < 0 ? -f : f;
        }

        public static float Max(float a, float b)
        {
            return a > b ? a : b;
        }

        public static float Min(float a, float b)
        {
            return a < b ? a : b;
        }

        public static float Sign(float f)
        {
            return f >= 0f ? 1f : -1f;
        }

        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Abs(target - current) <= maxDelta)
                return target;
            return current + Sign(target - current) * maxDelta;
        }
    }
}