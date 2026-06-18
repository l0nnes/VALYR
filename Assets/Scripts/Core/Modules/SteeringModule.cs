using System;
using Core.Attributes;
using Core.Configs;
using Core.Contexts;
using Core.Interfaces;
using Core.Math;

namespace Core.Modules
{
    [Serializable]
    public class SteeringModule : IVehicleModule
    {
        private Config _config = new();
        private bool _isInitialized;
        private float _trackWidth;
        private float _wheelBase;
        public VehicleModulePhase Phase => VehicleModulePhase.Steering;

        public void SetConfiguration(ModuleConfig config)
        {
            if (config is Config typedConfig) _config = typedConfig;
        }

        public void Initialize(VehicleContext context)
        {
            if (context.axles.Count < 2) return;

            // Расчет базы для Аккермана
            var frontZ = context.axles[0].leftWheel.state.localHardPoint.z;
            var rearZ = context.axles[^1].leftWheel.state.localHardPoint.z;
            _wheelBase = MathUtil.Abs(frontZ - rearZ);

            var leftX = context.axles[0].leftWheel.state.localHardPoint.x;
            var rightX = context.axles[0].rightWheel.state.localHardPoint.x;
            _trackWidth = MathUtil.Abs(leftX - rightX);

            _isInitialized = true;
        }

        public void Update(VehicleContext context, float dt)
        {
            var input = context.input.steering;

            foreach (var axle in context.axles)
            {
                if (axle.config.steeringMode == SteeringMode.Disable) continue;

                var targetAngle = input * _config.maxSteeringAngle;
                if (axle.config.steeringMode == SteeringMode.Reverse) targetAngle = -targetAngle;

                var currentAngle = axle.state.steerAngle;
                var speed = GetSteerSpeed(currentAngle, targetAngle);

                axle.state.steerAngle = MathUtil.MoveTowards(currentAngle, targetAngle, speed * dt);

                ApplySteeringToWheels(axle);
            }
        }

        private float GetSteerSpeed(float current, float target)
        {
            var sameSign = (current > 0 && target > 0) || (current < 0 && target < 0);
            var movingAway = sameSign && MathUtil.Abs(target) > MathUtil.Abs(current);
            var startingFromZero = MathUtil.Abs(current) < MathUtil.Epsilon && MathUtil.Abs(target) > MathUtil.Epsilon;

            return movingAway || startingFromZero ? _config.steerSpeed : _config.recenteringSpeed;
        }

        private void ApplySteeringToWheels(AxleContext axle)
        {
            var angle = axle.state.steerAngle;

            if (_config.ackermannFactor < MathUtil.Epsilon || !_isInitialized || MathUtil.Abs(angle) < 0.1f)
            {
                axle.leftWheel.state.steeringAngle = angle;
                axle.rightWheel.state.steeringAngle = angle;
                return;
            }

            float leftAckermann, rightAckermann;
            var absAngle = MathUtil.Abs(angle);
            var turnRadius = _wheelBase / (float)System.Math.Tan(absAngle * MathUtil.Deg2Rad);

            // Ackermann logic
            if (angle > 0)
            {
                rightAckermann = (float)System.Math.Atan(_wheelBase / (turnRadius - _trackWidth * 0.5f)) *
                                 MathUtil.Rad2Deg;
                leftAckermann = (float)System.Math.Atan(_wheelBase / (turnRadius + _trackWidth * 0.5f)) *
                                MathUtil.Rad2Deg;
            }
            else
            {
                leftAckermann = -(float)System.Math.Atan(_wheelBase / (turnRadius - _trackWidth * 0.5f)) *
                                MathUtil.Rad2Deg;
                rightAckermann = -(float)System.Math.Atan(_wheelBase / (turnRadius + _trackWidth * 0.5f)) *
                                 MathUtil.Rad2Deg;
            }

            axle.leftWheel.state.steeringAngle = MathUtil.Lerp(angle, leftAckermann, _config.ackermannFactor);
            axle.rightWheel.state.steeringAngle = MathUtil.Lerp(angle, rightAckermann, _config.ackermannFactor);
        }

        [Serializable]
        public class Config : ModuleConfig
        {
            [ConfigHeader("Angles")] [ConfigRange(10f, 60f)]
            public float maxSteeringAngle = 35f;

            [ConfigRange(0f, 1f)] public float ackermannFactor = 0.5f;

            [ConfigHeader("Response")] public float steerSpeed = 100f;

            public float recenteringSpeed = 200f;
        }
    }
}