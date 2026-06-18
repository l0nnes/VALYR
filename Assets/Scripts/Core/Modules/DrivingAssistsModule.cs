using System;
using Core.Attributes;
using Core.Configs;
using Core.Contexts;
using Core.Interfaces;
using Core.Math;

namespace Core.Modules
{
    /// <summary>
    ///     Simulates real-world vehicle electronic assists: ABS, TCS, and ESP.
    ///     Manages engine throttle reduction and individual wheel brake modulation.
    /// </summary>
    [Serializable]
    public class DrivingAssistsModule : IVehicleModule
    {
        private Config _config = new();

        public VehicleModulePhase Phase => VehicleModulePhase.InputModifiers;

        public void SetConfiguration(ModuleConfig config)
        {
            if (config is Config tConfig) _config = tConfig;
        }

        public void Initialize(VehicleContext context)
        {
            // Initial state reset if needed
        }

        public void Update(VehicleContext context, float dt)
        {
            var currentThrottle = context.input.throttle;
            var baseBrakeTorque = context.input.brake * _config.maxBrakeForce;

            var wheels = context.wheels;
            var wheelCount = wheels.Count;

            // 1. SENSOR PASS: Gather maximum slips for ECU logic
            var maxLatSlip = 0f;
            var maxForwardSlip = 0f;

            for (var i = 0; i < wheelCount; i++)
            {
                var state = wheels[i].state;
                if (!state.hit.isGrounded) continue;

                maxLatSlip = MathUtil.Max(maxLatSlip, MathUtil.Abs(state.tire.slipAngle));

                if (wheels[i].axle.config.isPowered)
                    maxForwardSlip = MathUtil.Max(maxForwardSlip, state.tire.slipRatio);
            }

            // 2. ESP LOGIC (Electronic Stability Program)
            // Real ESP limits throttle strictly when the car is sliding sideways to regain lateral grip.
            if (_config.enableESP && currentThrottle > 0.01f)
                if (maxLatSlip > _config.espSlipAngleThreshold)
                {
                    var severity = (maxLatSlip - _config.espSlipAngleThreshold) * _config.espThrottleSensitivity;
                    currentThrottle *= MathUtil.Clamp01(1f - severity);
                }

            // 3. TCS ENGINE LOGIC (Traction Control System - Throttle Cut)
            // Reduces engine output if driven wheels lose longitudinal traction.
            if (_config.enableTCS && currentThrottle > 0.01f)
                if (maxForwardSlip > _config.tcsSlipThreshold)
                {
                    var severity = (maxForwardSlip - _config.tcsSlipThreshold) * _config.tcsThrottleSensitivity;
                    currentThrottle *= MathUtil.Clamp01(1f - severity);
                }

            // Apply the modified throttle back to the ECU
            context.input.throttle = currentThrottle;

            // 4. ACTUATOR PASS: Individual wheel braking (ABS & TCS Brake)
            for (var i = 0; i < wheelCount; i++)
            {
                var wheel = wheels[i];
                var state = wheel.state;

                var appliedBrakeTorque = baseBrakeTorque;

                // ABS LOGIC (Anti-lock Braking System)
                // Real ABS modulates pressure proportionally using fast PWM valves, avoiding total 0% drops.
                if (_config.enableABS && baseBrakeTorque > 0.1f && state.hit.isGrounded)
                {
                    var slip = state.tire.slipRatio;

                    // Negative slip means the wheel is rotating slower than the road (sliding)
                    if (slip < -_config.absSlipThreshold)
                    {
                        var severity = (-slip - _config.absSlipThreshold) * _config.absSensitivity;
                        // Modulate brake smoothly instead of dropping it to 0
                        appliedBrakeTorque *= MathUtil.Clamp01(1f - severity);
                    }
                }

                // TCS BRAKE LOGIC (Electronic Differential / Brake-based Torque Vectoring)
                // If a wheel spins freely, brake it. This forces the mechanical differential to send torque to the wheel with grip.
                if (_config.enableTCS && wheel.axle.config.isPowered && state.hit.isGrounded &&
                    context.input.throttle > 0.01f)
                {
                    var slip = state.tire.slipRatio;
                    if (slip > _config.tcsBrakeSlipThreshold)
                    {
                        var severity = (slip - _config.tcsBrakeSlipThreshold) * _config.tcsBrakeSensitivity;
                        var tcsBrakeClamp = _config.maxBrakeForce * _config.tcsMaxBrakeForcePercent;

                        var tcsBrakeApplied = MathUtil.Clamp(severity * _config.maxBrakeForce, 0f, tcsBrakeClamp);

                        // ABS and TCS don't usually fight. Apply whichever requests more brake.
                        appliedBrakeTorque = MathUtil.Max(appliedBrakeTorque, tcsBrakeApplied);
                    }
                }

                // Apply final calculated brake force to the wheel
                state.brakeTorque = appliedBrakeTorque;
            }
        }

        [Serializable]
        public class Config : ModuleConfig
        {
            [ConfigSpace]
            [ConfigHeader("Brake System")]
            [ConfigTooltip("Maximum brake torque applied by the calipers (Nm).")]
            public float maxBrakeForce = 4000f;

            [ConfigSpace] [ConfigHeader("ABS (Anti-lock Braking System)")]
            public bool enableABS = true;

            [ConfigTooltip("Target slip ratio before ABS starts reducing brake pressure (absolute value).")]
            [ConfigRange(0.05f, 0.3f)]
            public float absSlipThreshold = 0.15f;

            [ConfigTooltip("How aggressively the ABS reduces brake pressure.")] [ConfigRange(1f, 20f)]
            public float absSensitivity = 8f;

            [ConfigSpace] [ConfigHeader("TCS (Traction Control System)")]
            public bool enableTCS = true;

            [ConfigTooltip("Forward slip ratio before TCS cuts engine throttle.")] [ConfigRange(0.05f, 0.5f)]
            public float tcsSlipThreshold = 0.15f;

            [ConfigTooltip("How aggressively TCS cuts the throttle.")] [ConfigRange(1f, 20f)]
            public float tcsThrottleSensitivity = 5f;

            [ConfigTooltip("Slip ratio before TCS applies brakes to the spinning wheel (Electronic Diff).")]
            [ConfigRange(0.1f, 0.8f)]
            public float tcsBrakeSlipThreshold = 0.25f;

            [ConfigTooltip("How aggressively TCS brakes the spinning wheel.")] [ConfigRange(1f, 10f)]
            public float tcsBrakeSensitivity = 3f;

            [ConfigTooltip("Max percent of total brake force TCS can use.")] [ConfigRange(0.1f, 1f)]
            public float tcsMaxBrakeForcePercent = 0.4f;

            [ConfigSpace] [ConfigHeader("ESP (Electronic Stability Program)")]
            public bool enableESP = true;

            [ConfigTooltip("Lateral slip angle (degrees) before ESP cuts throttle.")] [ConfigRange(2f, 30f)]
            public float espSlipAngleThreshold = 12f;

            [ConfigTooltip("How aggressively ESP cuts the throttle.")] [ConfigRange(0.1f, 2f)]
            public float espThrottleSensitivity = 0.1f;
        }
    }
}