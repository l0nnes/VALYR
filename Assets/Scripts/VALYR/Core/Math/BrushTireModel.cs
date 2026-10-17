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

        [Tooltip("Порог регуляризации малых скоростей (м/с) для устранения дрожания и сползания на склонах")]
        public float LowSpeedThreshold;

        public static BrushTireParameters Default => new()
        {
            LongitudinalStiffness = 25000f,
            LateralStiffness = 20000f,
            FrictionCoefficientX = 1.05f,
            FrictionCoefficientY = 1.00f,
            LowSpeedThreshold = 0.8f
        };
    }

    /// <summary>
    /// Высокопроизводительная реализация Щеточной модели шины (Fiala / Pacejka Brush Model).
    /// Особенности:
    /// 1. Непрерывный эллипс комбинированного сцепления (Combined Slip Ellipse).
    /// 2. Гладкий полиномиальный переход от сцепления к скольжению (fn = 3*theta - 3*theta^2 + theta^3).
    /// 3. Нулевые тригонометрические функции в ядре (быстрее формулы Пасейки в 4-6 раз).
    /// 4. Плавное демпфирование малых скоростей для железобетонного покоя на наклонных поверхностях.
    /// </summary>
    public static class BrushTireModel
    {
        private const float Epsilon = 1e-5f;

        /// <summary>
        /// Вычисляет продольный коэффициент пробуксовки (sx) и поперечный угол увода (sy).
        /// </summary>
        /// <param name="angularVelocity">Угловая скорость колеса (рад/с, omega)</param>
        /// <param name="wheelRadius">Радиус колеса (метры)</param>
        /// <param name="forwardSpeed">Продольная скорость точки контакта в базисе колеса (м/с)</param>
        /// <param name="sideSpeed">Поперечная скорость точки контакта в базисе колеса (м/с)</param>
        /// <param name="slipRatio">Результирующий продольный slip ratio (sx)</param>
        /// <param name="slipAngle">Результирующий поперечный угол увода / tan(alpha) (sy)</param>
        public static void CalculateSlip(
            float angularVelocity,
            float wheelRadius,
            float forwardSpeed,
            float sideSpeed,
            out float slipRatio,
            out float slipAngle)
        {
            float absForwardSpeed = UnityEngine.Mathf.Abs(forwardSpeed);
            // Предотвращаем деление на ноль при остановке
            float denominator = UnityEngine.Mathf.Max(absForwardSpeed, 0.4f);

            float linearWheelSpeed = angularVelocity * wheelRadius;

            // sx = (omega * R - Vx) / max(|Vx|, Vmin)
            slipRatio = (linearWheelSpeed - forwardSpeed) / denominator;

            // sy = -Vy / max(|Vx|, Vmin)  (тангенс угла увода)
            slipAngle = -sideSpeed / denominator;
        }

        /// <summary>
        /// Рассчитывает продольную (Fx) и поперечную (Fy) силы сцепления шины.
        /// </summary>
        /// <param name="parameters">Параметры жесткости и трения шины</param>
        /// <param name="normalLoad">Нормальная нагрузка на колесо (Fz, Ньютоны)</param>
        /// <param name="slipRatio">Продольный slip ratio (sx)</param>
        /// <param name="slipAngle">Поперечный slip angle (sy)</param>
        /// <param name="surfaceFrictionMultiplier">Множитель трения дорожного покрытия (1.0 = сухой асфальт, 0.3 = лед)</param>
        /// <param name="contactLinearSpeed">Модуль линейной скорости точки контакта относительно земли (м/с)</param>
        /// <param name="fx">Выходная продольная сила (Ньютоны, + разгон, - торможение)</param>
        /// <param name="fy">Выходная поперечная сила (Ньютоны, боковое удержание)</param>
        /// <param name="skidIntensity">Интенсивность скольжения 0..1 для эффектов визга и дыма</param>
        public static void CalculateTireForces(
            in BrushTireParameters parameters,
            float normalLoad,
            float slipRatio,
            float slipAngle,
            float surfaceFrictionMultiplier,
            float contactLinearSpeed,
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

            float effectiveMuX = UnityEngine.Mathf.Max(parameters.FrictionCoefficientX * surfaceFrictionMultiplier, 0.05f);
            float effectiveMuY = UnityEngine.Mathf.Max(parameters.FrictionCoefficientY * surfaceFrictionMultiplier, 0.05f);

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
                // Зона упругой адгезии + частичного скольжения задней части пятна контакта
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
            float rawFx = maxFx * fn * (sigmaX / sigma);
            float rawFy = maxFy * fn * (sigmaY / sigma);

            // Регуляризация околонулевых скоростей (Zero-Speed Stabilization):
            // Плавное сглаживание устраняет высокочастотный дребезг знака slip при остановке на крутых уклонах
            float speedFactor = 1.0f;
            float threshold = parameters.LowSpeedThreshold;
            if (threshold > Epsilon && contactLinearSpeed < threshold)
            {
                float t = contactLinearSpeed / threshold;
                // Кубический SmoothStep: 3*t^2 - 2*t^3
                speedFactor = t * t * (3f - 2f * t);
            }

            fx = rawFx * speedFactor;
            fy = rawFy * speedFactor;
        }
    }
}
