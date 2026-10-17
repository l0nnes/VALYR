using System;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Interfaces;
using VALYR.Core.Math;
using VALYR.Core.Modules;

namespace VALYR.Modules.Tires
{
    /// <summary>
    /// Модуль шин на основе аналитической модели щетки (Brush Tire Model).
    /// Вычисляет нелинейный эллипс комбинированного продольного и поперечного сцепления,
    /// предотвращает паразитное сползание на малых скоростях и решает дифференциальное уравнение
    /// динамики вращения каждого колеса (WheelDynamics).
    /// </summary>
    [Serializable]
    public class BrushTireModule : VehicleModule, ITireModule
    {
        [Header("Параметры передних шин")]
        public BrushTireParameters FrontTires = BrushTireParameters.Default;

        [Header("Параметры задних шин")]
        public BrushTireParameters RearTires = BrushTireParameters.Default;

        [Header("Инерция колеса")]
        [Tooltip("Момент инерции колеса и тормозного диска (кг*м^2, обычно 1.2..2.0)")]
        public float WheelInertia = 1.5f;

        public BrushTireModule()
        {
            ModuleId = "Tires.Brush";
        }

        public void SolveTires(
            ref VehicleState state,
            in WheelData4<WheelActuation> actuation,
            IPhysicsBody body,
            in VehicleChassisConfig chassis,
            float dt)
        {
            if (!IsEnabled)
                return;

            Quaternion bodyRot = body.Rotation;
            Vector3 bodyUp = bodyRot * Vector3.up;
            Vector3 bodyForward = bodyRot * Vector3.forward;

            for (int i = 0; i < 4; i++)
            {
                bool isFront = (i == 0 || i == 1);
                ref readonly BrushTireParameters tireParams = ref (isFront ? ref FrontTires : ref RearTires);

                float radius = chassis.WheelRadii[i];
                ref WheelState wheel = ref state.Wheels.GetRef(i);
                float driveTorque = actuation[i].GetEffectiveDriveTorque();
                float brakeTorque = actuation[i].GetEffectiveBrakeTorque();

                if (!wheel.HasContact || wheel.NormalLoad <= 0.001f)
                {
                    // Колесо в воздухе — свободное вращение
                    WheelDynamics.Integrate(
                        wheel.AngularVelocity,
                        wheel.RotationAngle,
                        driveTorque,
                        brakeTorque,
                        0f,
                        radius,
                        WheelInertia,
                        dt,
                        out float freeOmega,
                        out float freeAngle);

                    wheel.AngularVelocity = freeOmega;
                    wheel.RotationAngle = freeAngle;
                    wheel.SlipRatio = 0f;
                    wheel.SlipAngle = 0f;
                    wheel.TireForce = Vector3.zero;
                    wheel.SkidIntensity = 0f;
                    continue;
                }

                // Колесо касается поверхности: вычисляем локальный базис пятна контакта с учетом поворота руля
                Vector3 steeredDir = Quaternion.AngleAxis(Mathf.Rad2Deg * wheel.SteerAngle, bodyUp) * bodyForward;
                Vector3 wheelRight = Vector3.Cross(wheel.ContactNormal, steeredDir).normalized;
                Vector3 wheelForward = Vector3.Cross(wheelRight, wheel.ContactNormal).normalized;

                // Линейная скорость точки пятна контакта
                Vector3 contactVelocity = body.GetPointVelocity(wheel.ContactPoint);
                float vx = Vector3.Dot(contactVelocity, wheelForward);
                float vy = Vector3.Dot(contactVelocity, wheelRight);
                float contactLinearSpeed = contactVelocity.magnitude;

                // Расчет коэффициентов скольжения
                BrushTireModel.CalculateSlip(
                    wheel.AngularVelocity,
                    radius,
                    vx,
                    vy,
                    out float slipRatio,
                    out float slipAngle);

                // Расчет сил сцепления шины
                BrushTireModel.CalculateTireForces(
                    in tireParams,
                    wheel.NormalLoad,
                    slipRatio,
                    slipAngle,
                    1.0f,
                    contactLinearSpeed,
                    out float fx,
                    out float fy,
                    out float skidIntensity);

                // Приложение результирующей силы к телу автомобиля
                Vector3 worldTireForce = wheelForward * fx + wheelRight * fy;
                body.AddForceAtPosition(worldTireForce, wheel.ContactPoint);

                // Интеграция уравнения вращения колеса с учетом реакции шины (fx)
                WheelDynamics.Integrate(
                    wheel.AngularVelocity,
                    wheel.RotationAngle,
                    driveTorque,
                    brakeTorque,
                    fx,
                    radius,
                    WheelInertia,
                    dt,
                    out float nextOmega,
                    out float nextAngle);

                // Обновление состояния колеса
                wheel.AngularVelocity = nextOmega;
                wheel.RotationAngle = nextAngle;
                wheel.SlipRatio = slipRatio;
                wheel.SlipAngle = slipAngle;
                wheel.TireForce = worldTireForce;
                wheel.SkidIntensity = skidIntensity;
            }
        }
    }
}
