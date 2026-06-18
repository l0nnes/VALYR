using System;
using Core.Attributes;
using Core.Configs;
using Core.Contexts;
using Core.Interfaces;
using Core.Math;

namespace Core.Modules
{
    [Serializable]
    public class GearboxModule : IVehicleModule
    {
        private const float Rad2Rpm = 9.549296f;
        private bool _autoHoldActive;
        private float _brakeReverseTimer;

        private Config _config = new();
        private float _shiftCooldownTimer;
        private float _shiftTimer;

        public VehicleModulePhase Phase => VehicleModulePhase.Transmission;

        public void SetConfiguration(ModuleConfig config)
        {
            if (config is Config typed) _config = typed;
        }

        public void Initialize(VehicleContext context)
        {
            context.transmission.currentGear = _config.mode == GearboxMode.Automatic && !_config.startInNeutral ? 2 : 1;
            context.transmission.currentGearRatio = 0f;
            context.transmission.outputTorque = 0f;
            context.transmission.torque = 0f;
            context.transmission.isAutomatic = _config.mode == GearboxMode.Automatic;
            context.clutch.engagement = _config.mode == GearboxMode.Automatic ? 1f : 0f;

            _shiftTimer = 0f;
            _shiftCooldownTimer = 0f;
            _brakeReverseTimer = 0f;
            _autoHoldActive = false;
        }

        public void Update(VehicleContext context, float dt)
        {
            var dtSafe = MathUtil.Max(dt, 0.0001f);
            var transmission = context.transmission;

            UpdateAutomaticStopControls(context, dtSafe);
            ProcessShiftRequests(transmission, in context.input, context.engine, dtSafe);

            var ratio = GetCurrentRatio(transmission.currentGear) * _config.finalDriveRatio;
            transmission.currentGearRatio = ratio;
            transmission.isAutomatic = _config.mode == GearboxMode.Automatic;
            transmission.isShifting = _shiftTimer > 0f;
            transmission.shiftTimer = _shiftTimer;
            transmission.brakeReverseTimer = _brakeReverseTimer;
            transmission.isAutoHoldActive = _autoHoldActive;
            transmission.isBrakeReverseActive = transmission.isAutomatic && transmission.currentGear == 0;

            var avgDrivenWheelRpm = GetAverageDrivenWheelRpm(context);
            transmission.outputShaftRpm = avgDrivenWheelRpm * _config.finalDriveRatio;
            transmission.inputShaftRpm = ratio != 0f ? avgDrivenWheelRpm * MathUtil.Abs(ratio) : 0f;
            transmission.clutchSlip = context.engine.rpm - transmission.inputShaftRpm;

            UpdateClutch(context.clutch, transmission, context.engine, in context.input, dtSafe);
            ApplyOutputTorque(context);
            ApplyAutomaticBrakeOverrides(context);
        }

        private void UpdateAutomaticStopControls(VehicleContext context, float dt)
        {
            var transmission = context.transmission;
            if (_config.mode != GearboxMode.Automatic)
            {
                _brakeReverseTimer = 0f;
                _autoHoldActive = false;
                return;
            }

            var absSpeed = context.body.linearVelocity.Magnitude();
            var isStopped = absSpeed <= _config.stoppedSpeed;
            var brakePressed = context.input.brake >= _config.brakeToReverseInput;
            var throttlePressed = context.input.throttle >= _config.autoHoldReleaseThrottle;

            if (throttlePressed)
            {
                _brakeReverseTimer = 0f;
                _autoHoldActive = false;

                if (transmission.currentGear <= 1 && isStopped) ShiftTo(transmission, 2);

                return;
            }

            if (brakePressed)
            {
                _autoHoldActive = false;

                if (isStopped && transmission.currentGear != 0)
                {
                    _brakeReverseTimer += dt;
                    if (_brakeReverseTimer >= _config.brakeToReverseDelay) ShiftTo(transmission, 0);
                }
                else if (!isStopped)
                {
                    _brakeReverseTimer = 0f;
                }

                return;
            }

            if (isStopped && transmission.currentGear != 0 && _brakeReverseTimer > 0f &&
                _brakeReverseTimer < _config.brakeToReverseDelay)
            {
                _autoHoldActive = _config.enableAutoHold;
                if (transmission.currentGear == 1) transmission.currentGear = 2;
            }

            if (!_autoHoldActive || !isStopped) _brakeReverseTimer = 0f;
        }

        private void ProcessShiftRequests(TransmissionState transmission, in InputState input, EngineState engine,
            float dt)
        {
            if (_shiftTimer > 0f)
            {
                _shiftTimer = MathUtil.Max(0f, _shiftTimer - dt);
                return;
            }

            if (_shiftCooldownTimer > 0f) _shiftCooldownTimer = MathUtil.Max(0f, _shiftCooldownTimer - dt);

            var maxGear = GetMaxGearIndex();

            if (input.isUpshift && transmission.currentGear < maxGear)
            {
                ShiftTo(transmission, transmission.currentGear + 1);
                return;
            }

            if (input.isDownshift && transmission.currentGear > 0)
            {
                ShiftTo(transmission, transmission.currentGear - 1);
                return;
            }

            if (_config.mode != GearboxMode.Automatic || _shiftCooldownTimer > 0f || transmission.currentGear < 2)
                return;

            if (engine.rpm > _config.upshiftRpm && transmission.currentGear < maxGear)
            {
                var nextRpm = EstimateRpmAfterShift(transmission, transmission.currentGear + 1);
                if (nextRpm >= _config.minRpmAfterUpshift)
                {
                    ShiftTo(transmission, transmission.currentGear + 1);
                    return;
                }
            }

            if (engine.rpm < _config.downshiftRpm && transmission.currentGear > 2)
            {
                var lowerRpm = EstimateRpmAfterShift(transmission, transmission.currentGear - 1);
                if (lowerRpm < _config.maxRpmAfterDownshift) ShiftTo(transmission, transmission.currentGear - 1);
            }
        }

        private void ShiftTo(TransmissionState transmission, int gear)
        {
            transmission.currentGear = ClampGear(gear);
            _shiftTimer = _config.shiftDelay;
            _shiftCooldownTimer = _config.shiftCooldown;
        }

        private void UpdateClutch(ClutchState clutch, TransmissionState transmission, EngineState engine,
            in InputState input, float dt)
        {
            var target = 1f - MathUtil.Clamp01(input.clutch);

            if (_config.mode == GearboxMode.Automatic || _config.autoClutchAssist)
            {
                var rpmBlend = MathUtil.InverseLerp(_config.clutchEngageStartRpm,
                    _config.clutchEngageStartRpm + _config.clutchEngageRangeRpm,
                    engine.rpm);
                target = MathUtil.Min(target, rpmBlend);
            }

            if (transmission.currentGearRatio == 0f || _shiftTimer > 0f) target = 0f;

            clutch.engagement = MathUtil.MoveTowards(clutch.engagement, target, _config.clutchSpeed * dt);
        }

        private void ApplyOutputTorque(VehicleContext context)
        {
            var transmission = context.transmission;
            var clutch = context.clutch.engagement;

            if (transmission.currentGearRatio == 0f || clutch <= 0.001f || transmission.isShifting)
            {
                context.engine.loadTorque = 0f;
                transmission.torque = 0f;
                transmission.outputTorque = 0f;
                return;
            }

            var ratio = transmission.currentGearRatio;
            var shaftOmega = GetAverageDrivenWheelOmega(context) * ratio;
            var deltaOmega = context.engine.angularVelocity - shaftOmega;
            var clutchCapacity = _config.maxClutchTorque * clutch;
            var clutchTorque = MathUtil.Clamp(deltaOmega * _config.clutchStiffness, -clutchCapacity, clutchCapacity);

            var outputTorque = clutchTorque * ratio * _config.drivelineEfficiency;

            transmission.torque = clutchTorque;
            transmission.outputTorque = outputTorque;
            context.engine.loadTorque = clutchTorque;
        }

        private void ApplyAutomaticBrakeOverrides(VehicleContext context)
        {
            var transmission = context.transmission;
            if (_config.mode != GearboxMode.Automatic) return;

            if (_autoHoldActive)
            {
                ApplyMinimumBrakeTorque(context, _config.autoHoldBrakeTorque);
                transmission.outputTorque = 0f;
                return;
            }

            if (transmission.currentGear != 0) return;

            if (context.input.brake >= _config.brakeToReverseInput) ClearBrakeTorque(context);

            if (context.input.throttle >= _config.autoHoldReleaseThrottle)
                ApplyMinimumBrakeTorque(context, context.input.throttle * _config.reverseThrottleBrakeTorque);
        }

        private float EstimateRpmAfterShift(TransmissionState transmission, int targetGear)
        {
            var currentRatio = MathUtil.Abs(transmission.currentGearRatio);
            var targetRatio = MathUtil.Abs(GetCurrentRatio(targetGear) * _config.finalDriveRatio);
            if (currentRatio < 0.001f || targetRatio < 0.001f) return 0f;
            return MathUtil.Abs(transmission.inputShaftRpm) * targetRatio / currentRatio;
        }

        private float GetCurrentRatio(int currentGear)
        {
            if (currentGear == 0) return _config.reverseRatio;
            if (currentGear == 1) return 0f;

            var forwardIndex = currentGear - 2;
            if (_config.forwardGears == null || _config.forwardGears.Length == 0) return 0f;
            if (forwardIndex < 0 || forwardIndex >= _config.forwardGears.Length) return 0f;
            return _config.forwardGears[forwardIndex];
        }

        private int GetMaxGearIndex()
        {
            var forwardCount = _config.forwardGears != null ? _config.forwardGears.Length : 0;
            return forwardCount + 1 > 1 ? forwardCount + 1 : 1;
        }

        private int ClampGear(int gear)
        {
            var maxGear = GetMaxGearIndex();
            if (gear < 0) return 0;
            return gear > maxGear ? maxGear : gear;
        }

        private static void ClearBrakeTorque(VehicleContext context)
        {
            foreach (var wheel in context.wheels) wheel.state.brakeTorque = 0f;
        }

        private static void ApplyMinimumBrakeTorque(VehicleContext context, float brakeTorque)
        {
            foreach (var wheel in context.wheels)
                wheel.state.brakeTorque = MathUtil.Max(wheel.state.brakeTorque, brakeTorque);
        }

        private static float GetAverageDrivenWheelRpm(VehicleContext context)
        {
            var total = 0f;
            var count = 0;

            foreach (var wheel in context.wheels)
            {
                if (!wheel.axle.config.isPowered) continue;
                total += MathUtil.Abs(wheel.state.tire.angularVelocity) * Rad2Rpm;
                count++;
            }

            return count > 0 ? total / count : 0f;
        }

        private static float GetAverageDrivenWheelOmega(VehicleContext context)
        {
            var total = 0f;
            var count = 0;

            foreach (var wheel in context.wheels)
            {
                if (!wheel.axle.config.isPowered) continue;
                total += wheel.state.tire.angularVelocity;
                count++;
            }

            if (count > 0) return total / count;

            foreach (var wheel in context.wheels)
            {
                total += wheel.state.tire.angularVelocity;
                count++;
            }

            return count > 0 ? total / count : 0f;
        }

        [Serializable]
        public class Config : ModuleConfig
        {
            [ConfigHeader("Mode")] public GearboxMode mode = GearboxMode.Automatic;

            public bool startInNeutral;

            [ConfigHeader("Ratios")] public float reverseRatio = -3.3f;

            public float[] forwardGears = { 3.4f, 2.1f, 1.45f, 1.1f, 0.88f, 0.72f };
            public float finalDriveRatio = 3.7f;
            public float drivelineEfficiency = 0.9f;

            [ConfigHeader("Automatic Shift")] public float upshiftRpm = 6200f;

            public float downshiftRpm = 2400f;
            public float minRpmAfterUpshift = 3600f;
            public float maxRpmAfterDownshift = 6600f;
            public float shiftDelay = 0.2f;
            public float shiftCooldown = 0.65f;

            [ConfigHeader("Automatic Stop Controls")]
            public bool enableAutoHold = true;

            public float stoppedSpeed = 0.25f;
            public float brakeToReverseInput = 0.45f;
            public float brakeToReverseDelay = 0.45f;
            public float autoHoldReleaseThrottle = 0.08f;
            public float autoHoldBrakeTorque = 4500f;
            public float reverseThrottleBrakeTorque = 5000f;

            [ConfigHeader("Clutch")] public bool autoClutchAssist = true;

            public float clutchSpeed = 10f;
            public float clutchEngageStartRpm = 1000f;
            public float clutchEngageRangeRpm = 700f;
            public float maxClutchTorque = 950f;
            public float clutchStiffness = 18f;
        }
    }

    public enum GearboxMode
    {
        Manual,
        Automatic
    }
}