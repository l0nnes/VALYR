using System;

namespace Core.Math
{
    [Serializable]
    public struct TireSlipCurve
    {
        public SlipPoint[] points;

        public static TireSlipCurve DefaultLongitudinalFree => new()
        {
            points = new[]
            {
                new SlipPoint { slip = 0f, value = 0f },
                new SlipPoint { slip = 0.08f, value = 0.9f },
                new SlipPoint { slip = 0.16f, value = 1.12f },
                new SlipPoint { slip = 0.55f, value = 0.92f },
                new SlipPoint { slip = 1.0f, value = 0.78f }
            }
        };

        public static TireSlipCurve DefaultLongitudinalAtMaxLateral => new()
        {
            points = new[]
            {
                new SlipPoint { slip = 0f, value = 0f },
                new SlipPoint { slip = 0.12f, value = 0.55f },
                new SlipPoint { slip = 0.4f, value = 0.48f },
                new SlipPoint { slip = 1.0f, value = 0.38f }
            }
        };

        public static TireSlipCurve DefaultLateralFree => new()
        {
            points = new[]
            {
                new SlipPoint { slip = 0f, value = 0f },
                new SlipPoint { slip = 3f, value = 0.75f },
                new SlipPoint { slip = 7f, value = 1.08f },
                new SlipPoint { slip = 16f, value = 0.92f },
                new SlipPoint { slip = 32f, value = 0.76f }
            }
        };

        public static TireSlipCurve DefaultLateralAtMaxLongitudinal => new()
        {
            points = new[]
            {
                new SlipPoint { slip = 0f, value = 0f },
                new SlipPoint { slip = 5f, value = 0.45f },
                new SlipPoint { slip = 14f, value = 0.38f },
                new SlipPoint { slip = 32f, value = 0.28f }
            }
        };

        public float Evaluate(float slip)
        {
            slip = MathUtil.Abs(slip);
            if (points == null || points.Length == 0) return 0f;
            if (points.Length == 1) return MathUtil.Max(0f, points[0].value);

            var lower = points[0];
            var upper = points[0];
            var min = points[0];
            var max = points[0];
            var hasLower = false;
            var hasUpper = false;

            for (var i = 0; i < points.Length; i++)
            {
                var point = points[i];

                if (point.slip < min.slip) min = point;
                if (point.slip > max.slip) max = point;

                if (point.slip <= slip && (!hasLower || point.slip > lower.slip))
                {
                    lower = point;
                    hasLower = true;
                }

                if (point.slip >= slip && (!hasUpper || point.slip < upper.slip))
                {
                    upper = point;
                    hasUpper = true;
                }
            }

            if (!hasLower) return MathUtil.Max(0f, min.value);
            if (!hasUpper) return MathUtil.Max(0f, max.value);
            if (MathUtil.Abs(upper.slip - lower.slip) < MathUtil.Epsilon) return MathUtil.Max(0f, lower.value);

            var t = MathUtil.InverseLerp(lower.slip, upper.slip, slip);
            t = t * t * (3f - 2f * t);
            return MathUtil.Max(0f, MathUtil.Lerp(lower.value, upper.value, t));
        }
    }

    [Serializable]
    public struct SlipPoint
    {
        public float slip;
        public float value;
    }

    [Serializable]
    public class CombinedTireFrictionConfig
    {
        public TireSlipCurve longitudinalFree = TireSlipCurve.DefaultLongitudinalFree;
        public TireSlipCurve longitudinalAtMaxLateral = TireSlipCurve.DefaultLongitudinalAtMaxLateral;
        public TireSlipCurve lateralFree = TireSlipCurve.DefaultLateralFree;
        public TireSlipCurve lateralAtMaxLongitudinal = TireSlipCurve.DefaultLateralAtMaxLongitudinal;
        public float maxCombinedSlipRatio = 0.75f;
        public float maxCombinedSlipAngle = 22f;

        public float EvaluateLongitudinal(float slipRatio, float slipAngle)
        {
            var lateralBlend = MathUtil.Clamp01(MathUtil.Abs(slipAngle) / MathUtil.Max(maxCombinedSlipAngle, 0.001f));
            var free = longitudinalFree.Evaluate(slipRatio);
            var saturated = longitudinalAtMaxLateral.Evaluate(slipRatio);
            return MathUtil.Lerp(free, saturated, lateralBlend);
        }

        public float EvaluateLateral(float slipAngle, float slipRatio)
        {
            var longitudinalBlend =
                MathUtil.Clamp01(MathUtil.Abs(slipRatio) / MathUtil.Max(maxCombinedSlipRatio, 0.001f));
            var free = lateralFree.Evaluate(slipAngle);
            var saturated = lateralAtMaxLongitudinal.Evaluate(slipAngle);
            return MathUtil.Lerp(free, saturated, longitudinalBlend);
        }
    }
}