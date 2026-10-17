using System;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Modules;

namespace VALYR.Modules.Powertrain
{
    /// <summary>
    /// Модуль двигателя внутреннего сгорания (ДВС) с динамикой коленчатого вала,
    /// настраиваемой кривой крутящего момента, регулятором холостого хода и отсечкой (Rev Limiter).
    /// </summary>
    [Serializable]
    public class ArcadeEngineModule : VehicleModule, IEngineModule
    {
        [Header("Обороты двигателя (RPM)")]
        [Tooltip("Обороты холостого хода (RPM)")]
        public float IdleRpm = 850f;

        [Tooltip("Обороты срабатывания отсечки топлива (RPM)")]
        public float RevLimiterRpm = 7000f;

        [Tooltip("Максимальные допустимые обороты (RPM)")]
        public float MaxRpm = 7500f;

        [Header("Крутящий момент")]
        [Tooltip("Максимальный пиковый крутящий момент мотора (Нм)")]
        public float MaxTorque = 420f;

        [Tooltip("Момент сопротивления / торможения двигателем при сбросе газа (Нм)")]
        public float EngineBrakingTorque = 50f;

        [Tooltip("Момент инерции маховика и коленвала (кг*м^2, обычно 0.15..0.35)")]
        public float FlywheelInertia = 0.22f;

        [Header("Кривая крутящего момента")]
        [Tooltip("Нормализованная кривая крутящего момента (по X: нормализованные обороты 0..1, по Y: множитель момента 0..1)")]
        public AnimationCurve TorqueCurve = new(
            new Keyframe(0f, 0.45f),
            new Keyframe(0.2f, 0.75f),
            new Keyframe(0.55f, 1.0f),
            new Keyframe(0.85f, 0.90f),
            new Keyframe(1.0f, 0.70f)
        );

        private const float RadPerSecToRpm = 60f / (2f * Mathf.PI);

        public ArcadeEngineModule()
        {
            ModuleId = "Powertrain.Engine";
        }

        public override void Reset()
        {
            base.Reset();
        }

        public void UpdateEngine(
            in VehicleInput input,
            in TransmissionFeedback feedback,
            ref VehicleState state,
            float dt,
            out EngineOutput output)
        {
            if (!IsEnabled)
            {
                output = default;
                return;
            }

            float currentRpm = state.EngineRpm;
            if (currentRpm < IdleRpm)
                currentRpm = IdleRpm;

            float throttle = Mathf.Clamp01(input.Throttle);

            // 1. Проверка отсечки максимальных оборотов (Rev Limiter fuel cut)
            bool isRevLimiting = currentRpm >= RevLimiterRpm;
            float effectiveThrottle = isRevLimiting ? 0f : throttle;

            // 2. Расчет максимального момента на текущих оборотах
            float normalizedRpm = Mathf.Clamp01((currentRpm - IdleRpm) / Mathf.Max(1f, MaxRpm - IdleRpm));
            float torqueMultiplier = TorqueCurve.Evaluate(normalizedRpm);
            float availableTorque = MaxTorque * torqueMultiplier;

            // 3. Динамика оборотов двигателя
            if (!feedback.IsDisengaged && feedback.ClutchEngagement > 0.05f)
            {
                // Сцепление замкнуто: обороты коленвала физически синхронизируются с трансмиссией
                float targetRpm = Mathf.Max(feedback.DrivetrainRpm, IdleRpm);
                float syncRate = 18f * feedback.ClutchEngagement;
                currentRpm = Mathf.Lerp(currentRpm, targetRpm, 1f - Mathf.Exp(-syncRate * dt));

                // Двигатель отдает момент в трансмиссию
                float engineDriveTorque = availableTorque * effectiveThrottle;
                // Торможение двигателем при закрытом дросселе
                if (effectiveThrottle < 0.05f)
                {
                    engineDriveTorque -= EngineBrakingTorque * (currentRpm / MaxRpm);
                }

                output.OutputTorque = engineDriveTorque;
            }
            else
            {
                // Нейтраль или выжатое сцепление: свободная раскрутка маховика
                float accelTorque = availableTorque * effectiveThrottle;
                float dragTorque = (EngineBrakingTorque + 15f) * (currentRpm / MaxRpm);
                float netTorque = accelTorque - dragTorque;

                float deltaOmega = (netTorque / Mathf.Max(0.01f, FlywheelInertia)) * dt;
                float deltaRpm = deltaOmega * RadPerSecToRpm;
                currentRpm += deltaRpm;

                // В нейтрали момент на колеса не передается
                output.OutputTorque = 0f;
            }

            // Защита от заглохания: регулятор оборотов холостого хода (Idle Governor)
            if (currentRpm < IdleRpm)
            {
                currentRpm = Mathf.Lerp(currentRpm, IdleRpm, 1f - Mathf.Exp(-20f * dt));
            }

            currentRpm = Mathf.Clamp(currentRpm, IdleRpm, MaxRpm);
            state.EngineRpm = currentRpm;

            output.MaxTorque = availableTorque;
            output.EffectiveThrottle = effectiveThrottle;
        }
    }
}
