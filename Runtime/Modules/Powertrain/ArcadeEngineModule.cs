using System;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Modules;

namespace VALYR.Modules.Powertrain
{
    /// <summary>
    /// Internal combustion engine (ICE) module featuring crankshaft dynamics,
    /// customizable torque curve, idle governor, rev limiter, and torque converter fluid coupling.
    /// </summary>
    [Serializable]
    public class ArcadeEngineModule : VehicleModule, IEngineModule
    {
        [Header("RPM Limits")]
        [Tooltip("Engine idle speed in RPM (e.g. 700 - 850 RPM for road cars).")]
        [Range(500f, 1500f)]
        public float IdleRpm = 750f;

        [Tooltip("Fuel cut rev limiter threshold in RPM (e.g. 6200 - 7000 RPM).")]
        [Range(4000f, 10000f)]
        public float RevLimiterRpm = 6500f;

        [Tooltip("Absolute maximum allowable engine speed in RPM.")]
        [Range(5000f, 12000f)]
        public float MaxRpm = 6800f;

        [Tooltip("Torque converter stall speed in RPM with wheels locked at 100% throttle (typically 2000 - 2600 RPM for road automatics).")]
        [Range(1200f, 4000f)]
        public float StallRpm = 2300f;

        [Header("Torque & Power")]
        [Tooltip("Peak flywheel torque output in Newton-meters (Nm). Road car 1.6L: ~155 Nm, V8: ~450 Nm, Supercar: ~750 Nm.")]
        [Min(20f)]
        public float MaxTorque = 155f;

        [Tooltip("Passive engine braking drag torque when throttle is closed in gear (Nm).")]
        [Range(0f, 150f)]
        public float EngineBrakingTorque = 35f;

        [Tooltip("Flywheel and crankshaft rotational moment of inertia (kg*m^2). Lighter flywheels rev faster; heavier flywheels maintain momentum.")]
        [Range(0.05f, 0.8f)]
        public float FlywheelInertia = 0.16f;

        [Tooltip("Torque multiplication ratio of the hydrodynamic torque converter at maximum stall/slip (1.0 = no multiplication, 1.8 - 2.2 = standard automatic).")]
        [Range(1.0f, 2.5f)]
        public float TorqueConverterMultiplication = 1.85f;

        [Header("Torque Curve")]
        [Tooltip("Normalized torque curve. X-axis: normalized RPM (0 = Idle, 1 = Max RPM), Y-axis: torque multiplier (0..1).")]
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

            // 1. Fuel cut rev limiter check
            bool isRevLimiting = currentRpm >= RevLimiterRpm;
            float effectiveThrottle = isRevLimiting ? 0f : throttle;

            // 2. Evaluate available engine torque at current RPM
            float normalizedRpm = Mathf.Clamp01((currentRpm - IdleRpm) / Mathf.Max(1f, MaxRpm - IdleRpm));
            float torqueMultiplier = TorqueCurve.Evaluate(normalizedRpm);
            float availableTorque = MaxTorque * torqueMultiplier;

            // 3. Engine rotational dynamics
            if (!feedback.IsDisengaged && feedback.ClutchEngagement > 0.05f)
            {
                // Torque converter fluid coupling / launch clutch:
                // At low wheel speeds or standstill, engine revs up to stall target RPM matching throttle demand
                float stallTargetRpm = Mathf.Lerp(IdleRpm, StallRpm, effectiveThrottle);
                float targetRpm = Mathf.Max(feedback.DrivetrainRpm, stallTargetRpm);

                float syncRate = 18f * feedback.ClutchEngagement;
                currentRpm = Mathf.Lerp(currentRpm, targetRpm, 1f - Mathf.Exp(-syncRate * dt));

                // Fluid torque multiplication during high slip launch
                float speedRatio = Mathf.Clamp01(feedback.DrivetrainRpm / Mathf.Max(currentRpm, 1f));
                float torqueMultiplierConverter = Mathf.Lerp(TorqueConverterMultiplication, 1.0f, speedRatio);

                // Engine drive torque delivery
                float engineDriveTorque = (availableTorque * effectiveThrottle) * torqueMultiplierConverter;

                // Engine braking on overrun at speed with closed throttle
                if (effectiveThrottle < 0.05f && feedback.DrivetrainRpm > IdleRpm + 150f)
                {
                    engineDriveTorque -= EngineBrakingTorque * (currentRpm / MaxRpm);
                }
                else if (state.CurrentGear == 1 && effectiveThrottle < 0.05f && input.Brake < 0.05f && input.Handbrake < 0.05f && feedback.DrivetrainRpm < 350f)
                {
                    // Automatic transmission idle creep in 1st gear when all brakes are released
                    engineDriveTorque = 15f;
                }

                output.OutputTorque = engineDriveTorque;
            }
            else
            {
                // Disengaged clutch or neutral: free revving flywheel
                float accelTorque = availableTorque * effectiveThrottle;
                float dragTorque = (EngineBrakingTorque + 15f) * (currentRpm / MaxRpm);
                float netTorque = accelTorque - dragTorque;

                float deltaOmega = (netTorque / Mathf.Max(0.01f, FlywheelInertia)) * dt;
                float deltaRpm = deltaOmega * RadPerSecToRpm;
                currentRpm += deltaRpm;

                output.OutputTorque = 0f;
            }

            // Idle governor protection against stalling
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
