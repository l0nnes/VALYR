using System;
using UnityEngine;

namespace VALYR.Core.Math
{
    /// <summary>
    /// Решатель кинематики вращения колеса (Wheel Rotational Dynamics).
    /// Включает стандартный интегратор для свободного колеса в воздухе и
    /// безусловно устойчивый полунеявный интегратор (Unconditionally Stable Semi-Implicit Integrator)
    /// для колеса в контакте с землей, исключающий колебания и разворот колес на малых скоростях.
    /// </summary>
    public static class WheelDynamics
    {
        /// <summary>
        /// Интегрирует угловую скорость свободного колеса в воздухе.
        /// </summary>
        public static void Integrate(
            float currentOmega,
            float currentRotationAngle,
            float driveTorque,
            float brakeTorque,
            float tireLongitudinalForce,
            float wheelRadius,
            float wheelInertia,
            float dt,
            out float nextOmega,
            out float nextRotationAngle)
        {
            if (wheelInertia < 0.01f)
                wheelInertia = 0.01f;

            float tireReactionTorque = tireLongitudinalForce * wheelRadius;
            float maxBrakeToStop = (Mathf.Abs(currentOmega) * wheelInertia) / dt;
            float effectiveBrake = Mathf.Min(brakeTorque, maxBrakeToStop) * Mathf.Sign(currentOmega);
            float bearingDrag = 2.0f * currentOmega;

            float netTorque = driveTorque - tireReactionTorque - effectiveBrake - bearingDrag;
            float deltaOmega = (netTorque / wheelInertia) * dt;
            nextOmega = currentOmega + deltaOmega;
            nextRotationAngle = Mathf.Repeat(currentRotationAngle + nextOmega * dt, Mathf.PI * 2f);
        }

        /// <summary>
        /// Безусловно устойчивый полунеявный интегратор вращения колеса в контакте с дорогой.
        /// Решает проблему численной жесткости дифференциального уравнения при околонулевых скоростях.
        /// </summary>
        public static void IntegrateContact(
            float currentOmega,
            float currentRotationAngle,
            float driveTorque,
            float brakeTorque,
            float forwardSpeed,
            float wheelRadius,
            float wheelInertia,
            float longitudinalStiffness,
            float maxFx,
            float dt,
            out float nextOmega,
            out float nextRotationAngle,
            out float longitudinalSlip,
            out float appliedFx)
        {
            if (wheelInertia < 0.01f)
                wheelInertia = 0.01f;

            float r = wheelRadius;
            float vDenom = Mathf.Max(Mathf.Abs(forwardSpeed), 0.5f);

            // Тормозной момент: не должен вращать колесо в обратную сторону
            float maxBrake = (Mathf.Abs(currentOmega) * wheelInertia) / dt;
            float effectiveBrake = Mathf.Min(brakeTorque, maxBrake) * Mathf.Sign(currentOmega);
            float bearingDrag = 1.0f * currentOmega;

            float netExternalTorque = driveTorque - effectiveBrake - bearingDrag;

            // Коэффициент неявной жесткости: K = (Cx * R^2 * dt) / vDenom
            float kImplicit = (longitudinalStiffness * r * r * dt) / vDenom;

            // Целевая угловая скорость при нахождении в зоне упругого сцепления
            float targetOmega = (wheelInertia * currentOmega + netExternalTorque * dt + (longitudinalStiffness * r * dt / vDenom) * forwardSpeed) / (wheelInertia + kImplicit);

            // Продольный slip ratio на следующем шаге
            longitudinalSlip = (targetOmega * r - forwardSpeed) / vDenom;

            // Сила сцепления
            float rawFx = longitudinalStiffness * longitudinalSlip;

            if (Mathf.Abs(rawFx) > maxFx && maxFx > 0.001f)
            {
                // Проскальзывание / пробуксовка (выход за пределы эллипса сцепления)
                appliedFx = Mathf.Sign(rawFx) * maxFx;
                float netTorque = netExternalTorque - appliedFx * r;
                nextOmega = currentOmega + (netTorque / wheelInertia) * dt;
                longitudinalSlip = (nextOmega * r - forwardSpeed) / vDenom;
            }
            else
            {
                // Упругое сцепление (без срыва)
                appliedFx = rawFx;
                nextOmega = targetOmega;
            }

            nextRotationAngle = Mathf.Repeat(currentRotationAngle + nextOmega * dt, Mathf.PI * 2f);
        }
    }
}
