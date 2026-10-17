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

            // 1. Свободное вращение под действием привода, сопротивления подшипников и сцепления шины (без тормозов)
            float bearingDrag = 1.0f * currentOmega;
            float netDriveTorque = driveTorque - bearingDrag;

            // Коэффициент неявной жесткости: K = (Cx * R^2 * dt) / vDenom
            float kImplicit = (longitudinalStiffness * r * r * dt) / vDenom;
            float effectiveInertia = wheelInertia + kImplicit;

            // Свободная угловая скорость колеса до приложения дискового/стояночного тормоза
            float omegaFree = (wheelInertia * currentOmega + netDriveTorque * dt + (longitudinalStiffness * r * dt / vDenom) * forwardSpeed) / effectiveInertia;

            // 2. Точный расчет тормозного момента: если тормоз способен остановить колесо, он блокирует его (omega = 0)
            float torqueToStop = (Mathf.Abs(omegaFree) * effectiveInertia) / dt;
            float targetOmega;

            if (brakeTorque >= torqueToStop && brakeTorque > 0.001f)
            {
                // Тормоз полностью удерживает / блокирует колесо
                targetOmega = 0f;
            }
            else
            {
                // Тормоз замедляет колесо
                targetOmega = omegaFree - Mathf.Sign(omegaFree) * (brakeTorque * dt / effectiveInertia);
            }

            // Продольный slip ratio на следующем шаге
            longitudinalSlip = (targetOmega * r - forwardSpeed) / vDenom;

            // Сила сцепления
            float rawFx = longitudinalStiffness * longitudinalSlip;

            if (Mathf.Abs(rawFx) > maxFx && maxFx > 0.001f)
            {
                // Проскальзывание / пробуксовка (выход за пределы эллипса сцепления)
                appliedFx = Mathf.Sign(rawFx) * maxFx;

                float netTorque = driveTorque - appliedFx * r - bearingDrag;
                float freeAccel = netTorque / wheelInertia;
                float stopBrakeTorque = (Mathf.Abs(currentOmega + freeAccel * dt) * wheelInertia) / dt;
                float effectiveBrake = Mathf.Min(brakeTorque, stopBrakeTorque) * Mathf.Sign(currentOmega + freeAccel * dt);

                nextOmega = currentOmega + ((netTorque - effectiveBrake) / wheelInertia) * dt;
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
