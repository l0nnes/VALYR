using System;
using UnityEngine;

namespace VALYR.Core.Math
{
    /// <summary>
    /// Решатель кинематики вращения колеса (Wheel Rotational Dynamics).
    /// Решает дифференциальное уравнение:
    ///   I_wheel * d(omega)/dt = T_drive - T_brake * sign(omega) - F_x * R_wheel - T_rollingResistance
    /// Включает стабильное поглощение тормозного импульса около нуля (без ложного реверса вращения).
    /// </summary>
    public static class WheelDynamics
    {
        /// <summary>
        /// Интегрирует угловую скорость и угол вращения колеса за один физический шаг dt.
        /// </summary>
        /// <param name="currentOmega">Текущая угловая скорость колеса (рад/с)</param>
        /// <param name="currentRotationAngle">Текущий накопленный угол поворота (рад)</param>
        /// <param name="driveTorque">Крутящий момент привода (Нм)</param>
        /// <param name="brakeTorque">Эффективный тормозной момент (Нм, положительное число)</param>
        /// <param name="tireLongitudinalForce">Продольная сила сцепления шины с дорогой (Ньютоны, + тяга, - торможение)</param>
        /// <param name="wheelRadius">Радиус колеса (метры)</param>
        /// <param name="wheelInertia">Момент инерции колеса (кг*м^2, обычно 1.2 .. 2.5)</param>
        /// <param name="dt">Шаг времени (секунды)</param>
        /// <param name="nextOmega">Выходная новая угловая скорость (рад/с)</param>
        /// <param name="nextRotationAngle">Выходной новый угол вращения колеса (рад)</param>
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

            // Момент сопротивления от реакции сцепления шины с поверхностью
            float tireReactionTorque = tireLongitudinalForce * wheelRadius;

            // Тормозной момент: не должен заставлять колесо крутиться назад
            float maxBrakeToStop = (Mathf.Abs(currentOmega) * wheelInertia) / dt;
            float effectiveBrake = Mathf.Min(brakeTorque, maxBrakeToStop) * Mathf.Sign(currentOmega);

            // Момент сопротивления подшипников и аэродинамического трения свободного колеса
            float bearingDrag = 1.5f * currentOmega;

            // Результирующий крутящий момент
            float netTorque = driveTorque - tireReactionTorque - effectiveBrake - bearingDrag;

            // Простое и устойчивое интегрирование
            float deltaOmega = (netTorque / wheelInertia) * dt;
            nextOmega = currentOmega + deltaOmega;

            // Накопление угла для визуального вращения меша (циклический wrap [0..2*PI] исключает деградацию float)
            nextRotationAngle = Mathf.Repeat(currentRotationAngle + nextOmega * dt, Mathf.PI * 2f);
        }
    }
}
