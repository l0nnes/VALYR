using System;
using Core.Attributes;
using Core.Configs;
using Core.Contexts;
using Core.Interfaces;
using Core.Math;

namespace Core.Modules
{
    /// <summary>
    ///     Semicade engine module. The engine is always running in this version;
    ///     load can pull RPM down to idle, but it cannot stall the engine.
    /// </summary>
    [Serializable]
    public class EngineModule : IVehicleModule
    {
        private const float Rad2Rpm = 9.549296f;
        private const float Rpm2Rad = 0.104719755f;

        private Config _config = new();

        public VehicleModulePhase Phase => VehicleModulePhase.Engine;

        public void SetConfiguration(ModuleConfig config)
        {
            if (config is Config t) _config = t;
        }

        public void Initialize(VehicleContext context)
        {
            context.engine.isRunning = true;
            context.engine.rpm = _config.curve.idleRpm;
            context.engine.angularVelocity = context.engine.rpm * Rpm2Rad;
            context.engine.inertia = _config.inertia;
            context.engine.powerKw = 0f;
            context.engine.crankTimer = 0f;
        }

        public void Update(VehicleContext context, float dt)
        {
            var engine = context.engine;
            var input = context.input;

            var dtSafe = MathUtil.Max(dt, 0.0001f);
            var idleRpm = _config.curve.idleRpm;

            engine.isRunning = true;
            engine.crankTimer = 0f;

            var rpm = MathUtil.Max(engine.angularVelocity * Rad2Rpm, idleRpm);

            if (rpm > _config.cutoffRpm) engine.inCutoff = true;
            else if (rpm < _config.cutoffRpm - _config.cutoffHysteresis) engine.inCutoff = false;

            var throttleInput = input.throttle;
            if (context.transmission.isAutomatic && context.transmission.currentGear == 0) throttleInput = input.brake;

            if (context.transmission.isShifting) throttleInput = 0f;

            var throttle = engine.inCutoff ? 0f : MathUtil.Clamp01(throttleInput);
            var availableTorque = _config.curve.EvaluateTorqueNm(rpm);
            var combustionTorque = availableTorque * throttle;

            var idleError = idleRpm - rpm;
            if (idleError > 0f || (throttle < 0.05f && rpm < idleRpm + _config.idleControlWindow))
            {
                var idleTorque = (idleRpm + _config.idleControlWindow - rpm) * _config.idleGain;
                combustionTorque = MathUtil.Max(combustionTorque, MathUtil.Min(idleTorque, _config.maxIdleTorque));
            }

            var frictionTorque = _config.baseFriction
                                 + rpm * _config.frictionPerRpm
                                 + rpm * _config.closedThrottleBraking * (1f - throttle);

            var netTorque = combustionTorque - frictionTorque - engine.loadTorque;
            engine.angularVelocity += netTorque / _config.inertia * dtSafe;

            var idleOmega = idleRpm * Rpm2Rad;
            var maxOmega = _config.curve.maxRpm * Rpm2Rad;
            engine.angularVelocity = MathUtil.Clamp(engine.angularVelocity, idleOmega, maxOmega);

            engine.rpm = engine.angularVelocity * Rad2Rpm;
            engine.torque = combustionTorque;
            engine.powerKw = combustionTorque * engine.rpm / 9549.296f;
            engine.normalizedRpm = MathUtil.InverseLerp(idleRpm, _config.curve.maxRpm, engine.rpm);
        }

        [Serializable]
        public class Config : ModuleConfig
        {
            public EnginePowerCurve curve = new();

            [ConfigHeader("Physical Properties")] public float inertia = 0.25f;

            [ConfigHeader("Friction & Engine Braking")]
            public float baseFriction = 10f;

            public float frictionPerRpm = 0.003f;
            public float closedThrottleBraking = 0.01f;

            [ConfigHeader("Idle & Limiter")] public float idleGain = 1.5f;

            public float idleControlWindow = 120f;
            public float maxIdleTorque = 180f;
            public float cutoffRpm = 6800f;
            public float cutoffHysteresis = 180f;
        }
    }
}