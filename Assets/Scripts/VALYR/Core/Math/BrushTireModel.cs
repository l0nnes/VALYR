using System;
using UnityEngine;

namespace VALYR.Core.Math
{
    /// <summary>
    /// Настройки физических параметров щеточной модели шины (Brush Tire Model).
    /// </summary>
    [Serializable]
    public struct BrushTireParameters
    {
        [Tooltip("Продольная жесткость шины (Ньютоны на единицу slip, Cx)")]
        public float LongitudinalStiffness;

        [Tooltip("Поперечная жесткость увода шины (Ньютоны на радиан, Cy)")]
        public float LateralStiffness;

        [Tooltip("Базовый коэффициент продольного трения шины (mu_x)")]
        public float FrictionCoefficientX;

        [Tooltip("Базовый коэффициент поперечного трения шины (mu_y)")]
        public float FrictionCoefficientY;

        [Tooltip("Порог скорости для знаменателя скольжения (м/с)")]
        public float LowSpeedThreshold;

        public static BrushTireParameters Default => new()
        {
            LongitudinalStiffness = 35000f,
            LateralStiffness = 28000f,
            FrictionCoefficientX = 1.15f,
            FrictionCoefficientY = 1.10f,
            LowSpeedThreshold = 0.5f
        };
    }

    /// <summary>
    /// Высокопроизводительная реализация Щеточной модели шины (Fiala / Pacejka Brush Model).
    /// Особенности:
    /// 1. Непрерывный эллипс комбинированного сцепления (Combined Slip Ellipse).
    /// 2. Истинная статическая и кинематическая сила сцепления без искусственного обнуления на малых скоростях.
    /// 3. Симметричный учет продольной и поперечной скорости в знаменателе slip.
    /// 4. Нулевые аллокации в куче.
    /// </summary>
    public static class BrushTireModel
    {
        private const float Epsilon = 1e-5f;

        /// <summary>
        /// Вычисляет продольный коэффициент пробуксовки (sx) и поперечный угол увода (sy).
        /// </summary>
        public static void CalculateSlip(
            float angularVelocity,
            float wheelRadius,
            float forwardSpeed,
            float sideSpeed,
            out float slipRatio,
            out float slipAngle)
        {
            float linearWheelSpeed = angularVelocity * wheelRadius;
            // Истинная суммарная линейная скорость пятна контакта
            float contactSpeed = UnityEngine.Mathf.Sqrt(forwardSpeed * forwardSpeed + sideSpeed * sideSpeed);
            float denominator = UnityEngine.Mathf.Max(contactSpeed, 0.5f);

            // sx = (omega * R - Vx) / max(V_contact, V_min)
            slipRatio = (linearWheelSpeed - forwardSpeed) / denominator;

            // sy = -Vy / max(V_contact, V_min) (тангенс угла увода)
            slipAngle = -sideSpeed / denominator;
        }

        /// <summary>
        /// Рассчитывает продольную (Fx) и поперечную (Fy) силы сцепления шины.
        /// </summary>
        public static void CalculateTireForces(
            in BrushTireParameters parameters,
            float normalLoad,
            float slipRatio,
            float slipAngle,
            float surfaceFrictionMultiplier,
            out float fx,
            out float fy,
            out float skidIntensity)
        {
            // Если колесо оторвано от земли или нагрузка нулевая — сил сцепления нет
            if (normalLoad <= Epsilon)
            {
                fx = 0f;
                fy = 0f;
                skidIntensity = 0f;
                return;
            }

            float effectiveMuX = UnityEngine.Mathf.Max(parameters.FrictionCoefficientX * surfaceFrictionMultiplier, 0.1f);
            float effectiveMuY = UnityEngine.Mathf.Max(parameters.FrictionCoefficientY * surfaceFrictionMultiplier, 0.1f);

            float maxFx = effectiveMuX * normalLoad;
            float maxFy = effectiveMuY * normalLoad;

            // Безразмерные компоненты упругой деформации щетинок
            float sigmaX = (parameters.LongitudinalStiffness * slipRatio) / maxFx;
            float sigmaY = (parameters.LateralStiffness * slipAngle) / maxFy;

            // Результирующий вектор скольжения
            float sigma = UnityEngine.Mathf.Sqrt(sigmaX * sigmaX + sigmaY * sigmaY);

            if (sigma < Epsilon)
            {
                fx = 0f;
                fy = 0f;
                skidIntensity = 0f;
                return;
            }

            // theta = 1/3 * sigma. Переход в полное скольжение при theta >= 1.0 (sigma >= 3.0)
            float theta = sigma * (1f / 3f);
            float fn;

            if (theta < 1.0f)
            {
                // Зона упругой адгезии + частичного скольжения
                // fn = 1 - (1 - theta)^3 = theta * (3 - 3*theta + theta^2)
                fn = theta * (3f - 3f * theta + theta * theta);
                skidIntensity = theta;
            }
            else
            {
                // Полный срыв сцепления (Full Sliding / Drift / Burnout)
                fn = 1.0f;
                skidIntensity = 1.0f;
            }

            // Проекция на оси с учетом эллипса трения
            fx = maxFx * fn * (sigmaX / sigma);
            fy = maxFy * fn * (sigmaY / sigma);
        }
    }
}
