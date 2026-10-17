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
        public float IdleRpm = 750f;

        [Tooltip("Обороты срабатывания отсечки топлива (RPM)")]
        public float RevLimiterRpm = 6500f;

        [Tooltip("Максимальные допустимые обороты (RPM)")]
        public float MaxRpm = 6800f;

        [Tooltip("Обороты сваливания гидротрансформатора при заблокированных колесах и 100% газе (RPM)")]
        public float StallRpm = 2300f;

        [Header("Крутящий момент")]
        [Tooltip("Максимальный пиковый крутящий момент мотора (Нм)")]
        public float MaxTorque = 155f;

        [Tooltip("Момент сопротивления / торможения двигателем при сбросе газа (Нм)")]
        public float EngineBrakingTorque = 35f;

        [Tooltip("Момент инерции маховика и коленвала (кг*м^2, обычно 0.15..0.35)")]
        public float FlywheelInertia = 0.16f;

        [Tooltip("Коэффициент умножения момента гидротрансформатором при старте с места (1.8..2.2)")]
        public float TorqueConverterMultiplication = 1.85f;

        [Header("Кривая крутящего момента")]
        [Tooltip("Нормализованная кривая крутящего момента (по X: нормализованные обороты 0..1, по Y: множитель момента 0..1)")]
        public AnimationCurve TorqueCurve = new(
            new Keyframe(0f, 0.65f),
            new Keyframe(0.25f, 0.85f),
            new Keyframe(0.65f, 1.0f),
            new Keyframe(0.90f, 0.92f),
            new Keyframe(1.0f, 0.78f)
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

            float throttle = state.CurrentGear == -1 ? Mathf.Clamp01(input.Brake) : Mathf.Clamp01(input.Throttle);

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
                // Гидротрансформатор / эластичное сцепление старта:
                // При малых оборотах трансмиссии или остановке на передаче мотор развивает обороты сваливания (StallRpm)
                float stallTargetRpm = Mathf.Lerp(IdleRpm, StallRpm, effectiveThrottle);
                float targetRpm = Mathf.Max(feedback.DrivetrainRpm, stallTargetRpm);

                float syncRate = 18f * feedback.ClutchEngagement;
                currentRpm = Mathf.Lerp(currentRpm, targetRpm, 1f - Mathf.Exp(-syncRate * dt));

                // Умножение момента гидротрансформатором при трогании с места
                float speedRatio = Mathf.Clamp01(feedback.DrivetrainRpm / Mathf.Max(currentRpm, 1f));
                float torqueMultiplierConverter = Mathf.Lerp(TorqueConverterMultiplication, 1.0f, speedRatio);

                // Двигатель отдает момент в трансмиссию
                float engineDriveTorque = (availableTorque * effectiveThrottle) * torqueMultiplierConverter;

                // Торможение двигателем при закрытом дросселе на ходу
                if (effectiveThrottle < 0.05f && feedback.DrivetrainRpm > IdleRpm + 150f)
                {
                    engineDriveTorque -= EngineBrakingTorque * (currentRpm / MaxRpm);
                }
                else if (state.CurrentGear == 1 && effectiveThrottle < 0.05f && input.Brake < 0.05f && input.Handbrake < 0.05f && feedback.DrivetrainRpm < 350f)
                {
                    // Ползущий режим АКПП (Creep Mode) только на 1 передаче при полностью отпущенных тормозах и выключенном ручнике
                    engineDriveTorque = 15f;
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
