using System;
using Core.Attributes;

namespace Core.Math
{
    [Serializable]
    public class EnginePowerCurve
    {
        [ConfigHeader("RPM Range")] public float idleRpm = 900f;

        public float maxRpm = 7000f;

        [ConfigHeader("Torque Graph")] public TorquePoint[] points =
        {
            new() { rpm = 900f, torqueNm = 260f },
            new() { rpm = 1800f, torqueNm = 390f },
            new() { rpm = 3500f, torqueNm = 450f },
            new() { rpm = 5500f, torqueNm = 430f },
            new() { rpm = 7000f, torqueNm = 300f }
        };

        public float EvaluatePowerKw(float rpm)
        {
            return EvaluateTorqueNm(rpm) * MathUtil.Max(rpm, 0f) / 9549.296f;
        }

        public float EvaluateTorqueNm(float rpm)
        {
            if (rpm <= 0f) return 0f;
            if (points == null || points.Length == 0) return 0f;

            if (points.Length == 1) return MathUtil.Max(0f, points[0].torqueNm);

            var lower = points[0];
            var upper = points[0];
            var min = points[0];
            var max = points[0];
            var hasLower = false;
            var hasUpper = false;

            for (var i = 0; i < points.Length; i++)
            {
                var point = points[i];

                if (point.rpm < min.rpm) min = point;
                if (point.rpm > max.rpm) max = point;

                if (point.rpm <= rpm && (!hasLower || point.rpm > lower.rpm))
                {
                    lower = point;
                    hasLower = true;
                }

                if (point.rpm >= rpm && (!hasUpper || point.rpm < upper.rpm))
                {
                    upper = point;
                    hasUpper = true;
                }
            }

            if (!hasLower) return MathUtil.Max(0f, min.torqueNm);
            if (!hasUpper) return MathUtil.Max(0f, max.torqueNm);
            if (MathUtil.Abs(upper.rpm - lower.rpm) < MathUtil.Epsilon) return MathUtil.Max(0f, lower.torqueNm);

            var t = MathUtil.InverseLerp(lower.rpm, upper.rpm, rpm);
            t = t * t * (3f - 2f * t);
            return MathUtil.Max(0f, MathUtil.Lerp(lower.torqueNm, upper.torqueNm, t));
        }

        public float EvaluateHorsePower(float rpm)
        {
            return EvaluatePowerKw(rpm) * 1.341022f;
        }

        [Serializable]
        public struct TorquePoint
        {
            public float rpm;
            public float torqueNm;
        }
    }
}