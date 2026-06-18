using System;
using Core.Attributes;
using Core.Configs;
using Core.Contexts;
using Core.Interfaces;
using Core.Math;

namespace Core.Modules
{
    [Serializable]
    public class TireModule : IVehicleModule
    {
        private const float DefaultLowSpeedSlipReference = 1.5f;
        private const float DefaultLongitudinalSlipVelocityDeadZone = 0.04f;
        private const float DefaultLateralSlipVelocityDeadZone = 0.03f;
        private const float DefaultMaxSlipRatio = 2f;
        private const float DefaultVisualAngularVelocitySmoothing = 18f;
        private const float DefaultAirVisualAngularVelocitySmoothing = 10f;
        private const float DefaultVisualRollingBlendSlip = 0.08f;

        private Config _config = new();

        public VehicleModulePhase Phase => VehicleModulePhase.Tire;

        public void SetConfiguration(ModuleConfig config)
        {
            if (config is Config typedConfig) _config = typedConfig;
        }

        public void Initialize(VehicleContext context)
        {
        }

        public void Update(VehicleContext context, float dt)
        {
            foreach (var wheel in context.wheels) CalculateTire(context, wheel, MathUtil.Max(dt, 0.001f));
        }

        private void CalculateTire(VehicleContext context, WheelContext wheel, float dt)
        {
            var tire = wheel.state.tire;
            var wheelConfig = wheel.wheelConfig;

            var driveTorque = wheel.state.driveTorque;
            var brakeTorque = wheel.state.brakeTorque;
            if (context.input.handbrake && wheel.axle.index > 0)
                brakeTorque = MathUtil.Max(brakeTorque, _config.handbrakeTorque);

            if (!wheel.state.hit.isGrounded)
            {
                tire.longForce = 0f;
                tire.latForce = 0f;
                tire.totalForce = Vec3.Zero;
                tire.slipRatio = 0f;
                tire.slipAngle = 0f;
                tire.normalForce = 0f;
                tire.frictionUsage = 0f;
                ApplyWheelTorques(tire, wheelConfig, 0f, driveTorque, brakeTorque, 0f, tire.angularVelocity, dt, false);
                return;
            }

            var hasSoftContactNormalForce = wheel.state.hit.normalForce > MathUtil.Epsilon;

            var contactNormal = hasSoftContactNormalForce && wheel.state.hit.normal.SqrMagnitude() > MathUtil.Epsilon
                ? wheel.state.hit.normal.Normalize()
                : wheel.state.upDir;

            var forward = wheel.state.hit.contactForward.SqrMagnitude() > MathUtil.Epsilon
                ? wheel.state.hit.contactForward.Normalize()
                : Vec3.ProjectOnPlane(wheel.state.forwardDir, contactNormal).Normalize();

            var right = wheel.state.hit.contactRight.SqrMagnitude() > MathUtil.Epsilon
                ? wheel.state.hit.contactRight.Normalize()
                : Vec3.Cross(contactNormal, forward).Normalize();

            if (forward.SqrMagnitude() < MathUtil.Epsilon) forward = wheel.state.forwardDir;
            if (right.SqrMagnitude() < MathUtil.Epsilon) right = wheel.state.rightDir;

            var contactVelocity =
                context.host.GetPointVelocity(wheel.state.hit.point) - wheel.state.hit.surfaceVelocity;
            var vLong = Vec3.Dot(contactVelocity, forward);
            var vLat = Vec3.Dot(contactVelocity, right);
            var vWheel = tire.angularVelocity * wheelConfig.radius;

            var absVLong = MathUtil.Abs(vLong);
            var absVWheel = MathUtil.Abs(vWheel);
            var lowSpeedSlipReference = MathUtil.Max(_config.lowSpeedSlipReference, DefaultLowSpeedSlipReference);
            var longDeadZone = _config.longitudinalSlipVelocityDeadZone > 0f
                ? _config.longitudinalSlipVelocityDeadZone
                : DefaultLongitudinalSlipVelocityDeadZone;
            var lateralDeadZone = _config.lateralSlipVelocityDeadZone > 0f
                ? _config.lateralSlipVelocityDeadZone
                : DefaultLateralSlipVelocityDeadZone;
            var maxSlipRatio = MathUtil.Max(_config.maxSlipRatio, DefaultMaxSlipRatio);

            var denominator = MathUtil.Max(MathUtil.Max(absVWheel, absVLong), lowSpeedSlipReference);
            var longSlipVelocity = ApplyDeadZone(vWheel - vLong, longDeadZone);
            tire.slipRatio = MathUtil.Clamp(longSlipVelocity / denominator, -maxSlipRatio, maxSlipRatio);

            var lateralSlipVelocity = ApplyDeadZone(vLat, lateralDeadZone);
            var slipAngleRad =
                (float)System.Math.Atan2(lateralSlipVelocity, MathUtil.Max(absVLong, lowSpeedSlipReference));
            tire.slipAngle = slipAngleRad * MathUtil.Rad2Deg;

            var normalForce = GetNormalForce(wheel);
            tire.normalForce = normalForce;

            var longMu = _config.friction.EvaluateLongitudinal(tire.slipRatio, tire.slipAngle);
            var latMu = _config.friction.EvaluateLateral(tire.slipAngle, tire.slipRatio);

            var rawLongForce = MathUtil.Sign(tire.slipRatio) * longMu * normalForce;
            var rawLatForce = -MathUtil.Sign(tire.slipAngle) * latMu * normalForce;

            var targetOmega = vLong / MathUtil.Max(wheelConfig.radius, 0.001f);
            var deltaOmega = tire.angularVelocity - targetOmega;
            var torqueToSync = deltaOmega * wheelConfig.inertia / dt;
            var brakeSign = GetBrakeSign(tire.angularVelocity, vLong);
            var rollingResistanceTorque =
                GetRollingResistanceTorque(normalForce, wheelConfig.radius, tire.angularVelocity, vLong);
            var netAppliedWheelTorque = driveTorque - brakeTorque * brakeSign - rollingResistanceTorque;
            var demandLongForce = (torqueToSync + netAppliedWheelTorque) / MathUtil.Max(wheelConfig.radius, 0.001f);

            rawLongForce = LimitByDemand(rawLongForce, demandLongForce);
            rawLatForce = LimitLateralByVelocity(rawLatForce, normalForce, vLat, dt);

            var safetyMax = normalForce * _config.safetyGripMultiplier;
            var forceMagnitude = (float)System.Math.Sqrt(rawLongForce * rawLongForce + rawLatForce * rawLatForce);
            if (forceMagnitude > safetyMax && forceMagnitude > MathUtil.Epsilon)
            {
                var scale = safetyMax / forceMagnitude;
                rawLongForce *= scale;
                rawLatForce *= scale;
                forceMagnitude = safetyMax;
            }

            tire.longForce = rawLongForce;
            tire.latForce = rawLatForce;
            tire.totalForce = forward * rawLongForce + right * rawLatForce;
            tire.frictionUsage = safetyMax > MathUtil.Epsilon ? forceMagnitude / safetyMax : 0f;
            tire.rollingResistanceTorque = rollingResistanceTorque;

            ApplyWheelTorques(tire, wheelConfig, rawLongForce, driveTorque, brakeTorque, rollingResistanceTorque,
                targetOmega, dt, true);
        }

        private static float GetNormalForce(WheelContext wheel)
        {
            var contactNormalForce = MathUtil.Max(0f, wheel.state.hit.normalForce);
            var suspensionForce = MathUtil.Max(0f, wheel.state.suspension.force);
            return MathUtil.Max(contactNormalForce, suspensionForce);
        }

        private float GetRollingResistanceTorque(float normalForce, float radius, float omega, float vLong)
        {
            var sign = MathUtil.Abs(omega) > 0.1f ? MathUtil.Sign(omega) : MathUtil.Sign(vLong);
            return sign * normalForce * radius * _config.rollingResistance;
        }

        private static float GetBrakeSign(float omega, float vLong)
        {
            if (MathUtil.Abs(omega) > 0.1f) return MathUtil.Sign(omega);
            if (MathUtil.Abs(vLong) > 0.1f) return MathUtil.Sign(vLong);
            return 1f;
        }

        private static float LimitByDemand(float curveForce, float demandForce)
        {
            if (MathUtil.Abs(demandForce) < MathUtil.Epsilon) return curveForce;

            if (MathUtil.Sign(curveForce) == MathUtil.Sign(demandForce) &&
                MathUtil.Abs(curveForce) > MathUtil.Abs(demandForce))
                return demandForce;

            return curveForce;
        }

        private static float LimitLateralByVelocity(float lateralForce, float normalForce, float vLat, float dt)
        {
            if (normalForce <= MathUtil.Epsilon || MathUtil.Abs(vLat) <= MathUtil.Epsilon) return lateralForce;

            var massAtContact = normalForce / 9.81f;
            var syncForce = massAtContact * -vLat / dt;
            if (MathUtil.Sign(lateralForce) == MathUtil.Sign(syncForce) &&
                MathUtil.Abs(lateralForce) > MathUtil.Abs(syncForce))
                return syncForce;

            return lateralForce;
        }

        private static float ApplyDeadZone(float value, float deadZone)
        {
            var abs = MathUtil.Abs(value);
            if (abs <= deadZone) return 0f;
            return MathUtil.Sign(value) * (abs - deadZone);
        }

        private void ApplyWheelTorques(TireState tire, WheelConfig config, float longForce, float driveTorque,
            float brakeTorque, float rollingResistanceTorque, float rollingAngularVelocity, float dt, bool isGrounded)
        {
            var frictionTorque = longForce * config.radius;
            var netTorque = driveTorque - frictionTorque - rollingResistanceTorque;

            tire.angularAcceleration = netTorque / MathUtil.Max(config.inertia, 0.001f);
            tire.angularVelocity += tire.angularAcceleration * dt;

            if (brakeTorque > 0.01f)
            {
                var brakeDecay = brakeTorque / MathUtil.Max(config.inertia, 0.001f) * dt;
                if (MathUtil.Abs(tire.angularVelocity) <= brakeDecay)
                    tire.angularVelocity = 0f;
                else
                    tire.angularVelocity -= MathUtil.Sign(tire.angularVelocity) * brakeDecay;
            }

            var damping = isGrounded ? _config.groundedAngularDamping : _config.airAngularDamping;
            tire.angularVelocity *= MathUtil.Clamp01(1f - damping * dt);

            var targetVisualAngularVelocity = tire.angularVelocity;
            if (isGrounded)
            {
                var visualRollingBlendSlip = _config.visualRollingBlendSlip > 0f
                    ? _config.visualRollingBlendSlip
                    : DefaultVisualRollingBlendSlip;
                var slipBlend =
                    MathUtil.Clamp01(MathUtil.Abs(tire.slipRatio) / MathUtil.Max(visualRollingBlendSlip, 0.001f));
                targetVisualAngularVelocity = MathUtil.Lerp(rollingAngularVelocity, tire.angularVelocity, slipBlend);
            }

            var visualSmoothing = isGrounded
                ? _config.visualAngularVelocitySmoothing
                : _config.airVisualAngularVelocitySmoothing;
            if (visualSmoothing <= 0f)
                visualSmoothing = isGrounded
                    ? DefaultVisualAngularVelocitySmoothing
                    : DefaultAirVisualAngularVelocitySmoothing;
            var blend = MathUtil.Clamp01(1f - MathUtil.Exp(-visualSmoothing * dt));
            tire.visualAngularVelocity = MathUtil.Lerp(tire.visualAngularVelocity, targetVisualAngularVelocity, blend);
        }

        [Serializable]
        public class Config : ModuleConfig
        {
            [ConfigHeader("Combined Slip Friction")]
            public CombinedTireFrictionConfig friction = new();

            [ConfigHeader("Safety Limit")] public float safetyGripMultiplier = 1.15f;

            [ConfigHeader("Rolling Resistance")] public float rollingResistance = 0.015f;

            public float groundedAngularDamping = 0.02f;
            public float airAngularDamping = 0.12f;

            [ConfigHeader("Slip Stability")] public float lowSpeedSlipReference = DefaultLowSpeedSlipReference;

            public float longitudinalSlipVelocityDeadZone = DefaultLongitudinalSlipVelocityDeadZone;
            public float lateralSlipVelocityDeadZone = DefaultLateralSlipVelocityDeadZone;
            public float maxSlipRatio = DefaultMaxSlipRatio;

            [ConfigHeader("Visual Rotation")]
            public float visualAngularVelocitySmoothing = DefaultVisualAngularVelocitySmoothing;

            public float airVisualAngularVelocitySmoothing = DefaultAirVisualAngularVelocitySmoothing;
            public float visualRollingBlendSlip = DefaultVisualRollingBlendSlip;

            [ConfigHeader("Brakes")] public float handbrakeTorque = 5000f;
        }
    }
}