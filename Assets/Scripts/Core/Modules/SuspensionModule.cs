using System;
using Core.Attributes;
using Core.Configs;
using Core.Contexts;
using Core.Interfaces;
using Core.Math;

namespace Core.Modules
{
    [Serializable]
    public class SuspensionModule : IVehicleModule
    {
        private Config _config = new();

        private SuspensionData[] _data;
        public VehicleModulePhase Phase => VehicleModulePhase.Suspension;

        public void SetConfiguration(ModuleConfig config)
        {
            if (config is Config typedConfig) _config = typedConfig;
        }

        public void Initialize(VehicleContext context)
        {
            _data = new SuspensionData[context.wheels.Count];

            foreach (var wheel in context.wheels)
            {
                wheel.state.suspension.restLength = _config.restLength;
                wheel.state.suspension.currentLength = _config.restLength;
            }
        }

        public void Update(VehicleContext context, float dt)
        {
            foreach (var wheel in context.wheels) CalculateWheelSuspension(wheel, dt);
        }

        private void CalculateWheelSuspension(WheelContext wheel, float dt)
        {
            var state = wheel.state;
            ref var data = ref _data[wheel.index];

            if (!state.hit.isGrounded)
            {
                state.suspension.compressionRatio = 0f;
                state.suspension.currentLength = _config.restLength;
                state.suspension.force = 0f;
                state.worldPosition = state.worldHardPoint - state.upDir * _config.restLength;

                data.previousCompression = 0f;
                data.velocity = 0f;
                return;
            }

            var currentLength = state.hit.distance - wheel.wheelConfig.radius;
            currentLength = MathUtil.Clamp(currentLength, 0.001f, _config.restLength);

            var compression = 1.0f - currentLength / _config.restLength;
            compression = MathUtil.Clamp01(compression);

            var springForce = (_config.restLength - currentLength) * _config.springStiffness;

            if (compression > _config.bumpStopThreshold)
            {
                var overCompression = compression - _config.bumpStopThreshold;
                var bumpForce = overCompression * _config.restLength * _config.springStiffness *
                                _config.bumpStopMultiplier;
                springForce += bumpForce;
            }

            var previousLength = (1.0f - data.previousCompression) * _config.restLength;
            var rawVelocity = (previousLength - currentLength) / dt;

            var smoothFactor = MathUtil.Clamp01(1.0f - _config.velocitySmoothing);
            var velocity = MathUtil.Lerp(data.velocity, rawVelocity, smoothFactor);

            var damperForce = velocity >= 0
                ? velocity * _config.bumpDamper
                : velocity * _config.reboundDamper;

            state.suspension.compressionRatio = compression;
            state.suspension.currentLength = currentLength;
            state.suspension.force = MathUtil.Max(0f, springForce + damperForce);
            state.worldPosition = state.worldHardPoint - state.upDir * currentLength;

            data.previousCompression = compression;
            data.velocity = velocity;
        }

        [Serializable]
        public class Config : ModuleConfig
        {
            [ConfigHeader("Spring Settings")] [ConfigTooltip("Length of the suspension at rest (meters)")]
            public float restLength = 0.5f;

            [ConfigRange(1000, 100000)] public float springStiffness = 30000f;

            [ConfigHeader("Damping")] public float damperStiffness = 4000f;

            public float reboundDamper = 4500f;
            public float bumpDamper = 3000f;

            [ConfigSpace] [ConfigHeader("Limits")] [ConfigRange(0f, 1f)]
            public float bumpStopThreshold = 0.8f;

            public float bumpStopMultiplier = 5.0f;

            [ConfigRange(0f, 1f)] public float velocitySmoothing = 0.5f;
        }

        private struct SuspensionData
        {
            public float previousCompression;
            public float velocity;
        }
    }
}