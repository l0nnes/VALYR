using System;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Modules;

namespace VALYR.Modules.Powertrain
{
    public enum DrivetrainType
    {
        AllWheelDrive = 0,
        RearWheelDrive = 1,
        FrontWheelDrive = 2
    }

    /// <summary>
    /// Transmission and braking module supporting AWD, RWD, and FWD layouts,
    /// automatic shifting with cooldown & speed-gating, torque converter lockup,
    /// brake balance distribution, Auto Hold at standstill, and responsive Reverse gear logic.
    /// </summary>
    [Serializable]
    public class SimpleTransmissionModule : VehicleModule, ITransmissionModule
    {
        [Header("Drivetrain Layout")]
        [Tooltip("Drivetrain drive axle configuration: FWD (Front Wheel Drive), RWD (Rear Wheel Drive), or AWD (All Wheel Drive).")]
        public DrivetrainType Drivetrain = DrivetrainType.FrontWheelDrive;

        [Tooltip("Front-to-rear torque split for AWD (e.g. 0.50 = 50% front, 50% rear). Only applies when Drivetrain is AllWheelDrive.")]
        [Range(0f, 1f)]
        public float FrontTorqueSplitAwd = 0.50f;

        [Header("Gear Ratios")]
        [Tooltip("Reverse gear ratio (negative value, e.g. -3.487).")]
        public float ReverseGearRatio = -3.487f;

        [Tooltip("Forward gear ratios array (1st, 2nd, ... Nth gear).")]
        public float[] ForwardGearRatios = { 3.612f, 2.085f, 1.370f, 1.039f, 0.778f, 0.633f };

        [Tooltip("Final drive differential gear reduction ratio (e.g. 3.943). Multiplies each gear ratio.")]
        [Min(0.5f)]
        public float FinalDriveRatio = 3.943f;

        [Tooltip("Mechanical efficiency of the transmission and driveline (typically 0.88 - 0.94).")]
        [Range(0.7f, 1f)]
        public float TransmissionEfficiency = 0.90f;

        [Header("Automatic Shifting")]
        [Tooltip("Enable automatic gear shifting.")]
        public bool AutomaticShift = true;

        [Tooltip("Engine RPM threshold to initiate an upshift.")]
        [Range(3000f, 9000f)]
        public float ShiftUpRpm = 5800f;

        [Tooltip("Engine RPM threshold to initiate a downshift.")]
        [Range(1000f, 4000f)]
        public float ShiftDownRpm = 1900f;

        [Tooltip("Duration of the power interrupt during forward gear shifting in seconds (0.15 - 0.35s).")]
        [Range(0.05f, 0.6f)]
        public float ShiftDuration = 0.25f;

        [Header("Braking System")]
        [Tooltip("Maximum service brake torque per wheel at 100% brake pedal input (Nm).")]
        [Min(100f)]
        public float MaxBrakeTorque = 1800f;

        [Tooltip("Brake force bias distribution (e.g. 0.72 = 72% front axle, 28% rear axle). FWD cars typically run 68-75% front bias.")]
        [Range(0.4f, 0.9f)]
        public float FrontBrakeBias = 0.72f;

        [Tooltip("Maximum parking handbrake torque applied exclusively to the rear axle (Nm).")]
        [Min(100f)]
        public float MaxHandbrakeTorque = 2400f;

        [Header("Auto Hold System")]
        [Tooltip("Enable Auto Hold: holds vehicle brakes when coming to a complete stop until throttle is pressed.")]
        public bool EnableAutoHold = true;

        [Tooltip("Throttle input threshold above which Auto Hold disengages and the vehicle accelerates forward.")]
        [Range(0.01f, 0.3f)]
        public float AutoHoldThrottleThreshold = 0.08f;

        [Tooltip("Brake input threshold to engage reverse or disengage Auto Hold while stopped.")]
        [Range(0.01f, 0.3f)]
        public float AutoHoldBrakeThreshold = 0.08f;

        [Tooltip("Holding brake torque applied to all wheels when Auto Hold is active (Nm).")]
        [Min(200f)]
        public float AutoHoldBrakeTorque = 1600f;

        [Header("Reverse Gear Tuning")]
        [Tooltip("Maximum allowed speed in reverse (km/h) to prevent uncontrollable backward runaway.")]
        [Range(20f, 80f)]
        public float MaxReverseSpeedKmh = 42f;

        [Tooltip("Delay in seconds when holding brake at standstill before shifting into reverse (typical: 0.15 - 0.30s).")]
        [Range(0.05f, 0.6f)]
        public float ReverseHoldDelay = 0.20f;

        [Tooltip("Brief shift duration when entering or leaving reverse (seconds).")]
        [Range(0.05f, 0.3f)]
        public float ReverseShiftDuration = 0.12f;

        private const float RadPerSecToRpm = 60f / (2f * Mathf.PI);
        private float _reverseHoldTimer;
        private float _shiftCooldownTimer;
        private bool _isAutoHoldActive;

        public bool IsAutoHoldActive => _isAutoHoldActive;

        public SimpleTransmissionModule()
        {
            ModuleId = "Powertrain.Transmission";
        }

        public void UpdateTransmission(
            in VehicleInput input,
            in EngineOutput engineOutput,
            ref VehicleState state,
            ref WheelData4<WheelActuation> actuation,
            in VehicleChassisConfig chassis,
            float dt,
            out TransmissionFeedback feedback)
        {
            if (!IsEnabled)
            {
                feedback = TransmissionFeedback.Free;
                return;
            }

            // Default gear initialization on start
            if (state.CurrentGear == 0)
            {
                state.CurrentGear = 1;
                state.ClutchEngagement = 1f;
            }

            if (_shiftCooldownTimer > 0f)
            {
                _shiftCooldownTimer -= dt;
            }

            // 1. Shift duration timer & engine rev-matching
            if (state.ShiftTimer > 0f)
            {
                state.ShiftTimer -= dt;
                state.ClutchEngagement = 0.05f;

                // Rev-match engine speed to the incoming gear ratio during shift
                float nextGearRatio = GetGearRatio(state.CurrentGear);
                float targetGearRpm = Mathf.Abs(GetDrivenWheelsAverageAngularVelocity(in state) * nextGearRatio * FinalDriveRatio) * RadPerSecToRpm;
                state.EngineRpm = Mathf.Lerp(state.EngineRpm, Mathf.Max(targetGearRpm, 750f), 1f - Mathf.Exp(-14f * dt));

                if (state.ShiftTimer <= 0f)
                {
                    state.ShiftTimer = 0f;
                    state.ClutchEngagement = 1f;
                }
            }
            else
            {
                state.ClutchEngagement = 1f;
            }

            float vehicleGroundSpeed = GetVehicleGroundSpeed(in state, in chassis);

            // 2. Automatic gear shifting logic & Auto Hold state machine
            if (AutomaticShift && state.ShiftTimer <= 0f && _shiftCooldownTimer <= 0f)
            {
                UpdateAutoShift(in input, ref state, in chassis, vehicleGroundSpeed, dt);
            }

            // 3. Compute effective total gear ratio
            float currentRatio = GetGearRatio(state.CurrentGear);
            float totalRatio = currentRatio * FinalDriveRatio;

            // 4. Determine drive demand and brake demand depending on mode (Forward, Reverse, AutoHold)
            float driveDemand = 0f;
            float brakeDemand = 0f;

            if (_isAutoHoldActive)
            {
                // Under Auto Hold: zero drive demand, holding brake applied below
                driveDemand = 0f;
                brakeDemand = 0f;
            }
            else if (state.CurrentGear == -1)
            {
                // In Reverse: Brake pedal is throttle, Throttle pedal is brake
                driveDemand = Mathf.Clamp01(input.Brake);
                brakeDemand = Mathf.Clamp01(input.Throttle);

                // Speed governor in reverse
                float backwardSpeedKmh = vehicleGroundSpeed * 3.6f;
                if (backwardSpeedKmh > MaxReverseSpeedKmh)
                {
                    float overspeed = backwardSpeedKmh - MaxReverseSpeedKmh;
                    driveDemand *= Mathf.Clamp01(1f - overspeed / 5f);
                }
            }
            else
            {
                // Forward Gears: Throttle is drive, Brake is brake
                driveDemand = Mathf.Clamp01(input.Throttle);
                brakeDemand = Mathf.Clamp01(input.Brake);
            }

            float netDriveTorque = engineOutput.OutputTorque * totalRatio * TransmissionEfficiency * state.ClutchEngagement;

            // Torque distribution by drivetrain type (FL=0, FR=1, RL=2, RR=3)
            float frontRatio;
            float rearRatio;

            switch (Drivetrain)
            {
                case DrivetrainType.FrontWheelDrive:
                    frontRatio = 1.0f;
                    rearRatio = 0.0f;
                    break;
                case DrivetrainType.RearWheelDrive:
                    frontRatio = 0.0f;
                    rearRatio = 1.0f;
                    break;
                case DrivetrainType.AllWheelDrive:
                default:
                    frontRatio = FrontTorqueSplitAwd;
                    rearRatio = 1.0f - FrontTorqueSplitAwd;
                    break;
            }

            float torquePerFrontWheel = (netDriveTorque * frontRatio) * 0.5f;
            float torquePerRearWheel = (netDriveTorque * rearRatio) * 0.5f;

            // Airborne wheel overspin protection (do not accelerate wheels faster than redline gear speed)
            float maxWheelOmega = Mathf.Abs(totalRatio) > 0.01f
                ? (7200f / RadPerSecToRpm) / Mathf.Abs(totalRatio)
                : 200f;

            actuation.GetRef(0).DriveTorque += (Mathf.Abs(state.Wheels[0].AngularVelocity) < maxWheelOmega) ? torquePerFrontWheel : 0f; // FL
            actuation.GetRef(1).DriveTorque += (Mathf.Abs(state.Wheels[1].AngularVelocity) < maxWheelOmega) ? torquePerFrontWheel : 0f; // FR
            actuation.GetRef(2).DriveTorque += (Mathf.Abs(state.Wheels[2].AngularVelocity) < maxWheelOmega) ? torquePerRearWheel : 0f;  // RL
            actuation.GetRef(3).DriveTorque += (Mathf.Abs(state.Wheels[3].AngularVelocity) < maxWheelOmega) ? torquePerRearWheel : 0f;  // RR

            // 5. Brake torque distribution
            float totalBrakeTorque = brakeDemand * MaxBrakeTorque;

            float frontBrakeTorque = (totalBrakeTorque * FrontBrakeBias) * 0.5f;
            float rearBrakeTorque = (totalBrakeTorque * (1f - FrontBrakeBias)) * 0.5f;

            // Apply Auto Hold holding torque when active
            if (_isAutoHoldActive)
            {
                float holdTorque = AutoHoldBrakeTorque * 0.5f;
                frontBrakeTorque += holdTorque;
                rearBrakeTorque += holdTorque;
            }

            actuation.GetRef(0).BrakeTorque += frontBrakeTorque;
            actuation.GetRef(1).BrakeTorque += frontBrakeTorque;
            actuation.GetRef(2).BrakeTorque += rearBrakeTorque;
            actuation.GetRef(3).BrakeTorque += rearBrakeTorque;

            // Parking handbrake applied to rear axle only
            float handbrakeDemand = Mathf.Clamp01(input.Handbrake);
            float handbrakeTorquePerRearWheel = (handbrakeDemand * MaxHandbrakeTorque) * 0.5f;

            actuation.GetRef(2).HandbrakeTorque += handbrakeTorquePerRearWheel;
            actuation.GetRef(3).HandbrakeTorque += handbrakeTorquePerRearWheel;

            // 6. Build feedback for engine
            float drivenWheelsSpeed = GetDrivenWheelsAverageAngularVelocity(in state);
            float drivetrainRpm = Mathf.Abs(drivenWheelsSpeed * totalRatio) * RadPerSecToRpm;

            feedback = new TransmissionFeedback
            {
                DrivetrainRpm = drivetrainRpm,
                LoadTorque = 0f,
                ClutchEngagement = state.ClutchEngagement,
                IsDisengaged = (state.CurrentGear == 0 || state.ShiftTimer > 0f || _isAutoHoldActive)
            };
        }

        private float GetGearRatio(int gear)
        {
            if (gear == -1)
                return ReverseGearRatio;
            if (gear <= 0 || ForwardGearRatios == null || ForwardGearRatios.Length == 0)
                return 0f;

            int index = Mathf.Clamp(gear - 1, 0, ForwardGearRatios.Length - 1);
            return ForwardGearRatios[index];
        }

        private void UpdateAutoShift(in VehicleInput input, ref VehicleState state, in VehicleChassisConfig chassis, float vehicleGroundSpeed, float dt)
        {
            float rpm = state.EngineRpm;
            int gear = state.CurrentGear;

            // A. Auto Hold State Machine
            if (EnableAutoHold)
            {
                if (vehicleGroundSpeed < 0.35f)
                {
                    // Engage Auto Hold when coming to a complete stop and releasing pedals
                    if (!_isAutoHoldActive && input.Brake < AutoHoldBrakeThreshold && input.Throttle < AutoHoldThrottleThreshold)
                    {
                        _isAutoHoldActive = true;
                    }

                    if (_isAutoHoldActive)
                    {
                        // 1. Pressing throttle disengages Auto Hold and moves forward
                        if (input.Throttle > AutoHoldThrottleThreshold)
                        {
                            _isAutoHoldActive = false;
                            if (state.CurrentGear == -1)
                            {
                                state.CurrentGear = 1;
                                state.ShiftTimer = ReverseShiftDuration;
                                _shiftCooldownTimer = 0.25f;
                            }
                            return;
                        }

                        // 2. Pressing brake while stopped disengages Auto Hold and immediately shifts into Reverse
                        if (input.Brake > AutoHoldBrakeThreshold)
                        {
                            _isAutoHoldActive = false;
                            if (state.CurrentGear >= 1)
                            {
                                state.CurrentGear = -1;
                                state.ShiftTimer = ReverseShiftDuration;
                                _shiftCooldownTimer = 0.25f;
                            }
                            return;
                        }

                        return;
                    }
                }
                else
                {
                    _isAutoHoldActive = false;
                }
            }

            // B. Shifting into Reverse by holding brake at standstill
            if (gear >= 1 && vehicleGroundSpeed < 0.35f && input.Brake > 0.25f && input.Throttle < AutoHoldThrottleThreshold)
            {
                _reverseHoldTimer += dt;
                if (_reverseHoldTimer > ReverseHoldDelay)
                {
                    state.CurrentGear = -1;
                    state.ShiftTimer = ReverseShiftDuration;
                    _shiftCooldownTimer = 0.25f;
                    _reverseHoldTimer = 0f;
                    _isAutoHoldActive = false;
                    return;
                }
            }
            else
            {
                _reverseHoldTimer = 0f;
            }

            // C. Shifting from Reverse back to 1st gear on throttle press while vehicle is stopped
            if (gear == -1 && input.Throttle > AutoHoldThrottleThreshold && vehicleGroundSpeed < 0.45f)
            {
                state.CurrentGear = 1;
                state.ShiftTimer = ReverseShiftDuration;
                _shiftCooldownTimer = 0.25f;
                _isAutoHoldActive = false;
                return;
            }

            // D. Upshift (1 -> 2 -> ... -> N):
            // Gated by realistic physical speed limits for each gear to prevent skipping
            if (gear >= 1 && ForwardGearRatios != null && gear < ForwardGearRatios.Length)
            {
                float minSpeedForUpshift = GetGearMaxSpeed(gear, in chassis) * 0.78f;
                if (rpm > ShiftUpRpm && vehicleGroundSpeed > minSpeedForUpshift)
                {
                    state.CurrentGear = gear + 1;
                    state.ShiftTimer = ShiftDuration;
                    _shiftCooldownTimer = 0.8f;
                    return;
                }
            }

            // E. Downshift (N -> ... -> 2 -> 1)
            if (gear > 1)
            {
                float minSpeedForCurrentGear = GetGearMaxSpeed(gear - 1, in chassis) * 0.65f;
                if (rpm < ShiftDownRpm || vehicleGroundSpeed < minSpeedForCurrentGear)
                {
                    state.CurrentGear = gear - 1;
                    state.ShiftTimer = ShiftDuration;
                    _shiftCooldownTimer = 0.6f;
                }
            }
        }

        private float GetGearMaxSpeed(int gear, in VehicleChassisConfig chassis)
        {
            float ratio = GetGearRatio(gear);
            if (Mathf.Abs(ratio) <= 0.01f) return 100f;
            float totalRatio = Mathf.Abs(ratio * FinalDriveRatio);
            float wheelRadius = chassis.WheelRadii[0];
            float maxWheelOmega = (ShiftUpRpm / RadPerSecToRpm) / totalRatio;
            return maxWheelOmega * wheelRadius;
        }

        private float GetVehicleGroundSpeed(in VehicleState state, in VehicleChassisConfig chassis)
        {
            if (Drivetrain == DrivetrainType.FrontWheelDrive)
            {
                // Non-driven rear wheels freely roll and measure true ground speed
                return (Mathf.Abs(state.Wheels[2].AngularVelocity) * chassis.WheelRadii[2] +
                        Mathf.Abs(state.Wheels[3].AngularVelocity) * chassis.WheelRadii[3]) * 0.5f;
            }
            if (Drivetrain == DrivetrainType.RearWheelDrive)
            {
                // Non-driven front wheels freely roll and measure true ground speed
                return (Mathf.Abs(state.Wheels[0].AngularVelocity) * chassis.WheelRadii[0] +
                        Mathf.Abs(state.Wheels[1].AngularVelocity) * chassis.WheelRadii[1]) * 0.5f;
            }

            // For AWD, pick the lower speed between axles to avoid wheelspin false readings
            float frontSpeed = (Mathf.Abs(state.Wheels[0].AngularVelocity) * chassis.WheelRadii[0] +
                                Mathf.Abs(state.Wheels[1].AngularVelocity) * chassis.WheelRadii[1]) * 0.5f;
            float rearSpeed = (Mathf.Abs(state.Wheels[2].AngularVelocity) * chassis.WheelRadii[2] +
                               Mathf.Abs(state.Wheels[3].AngularVelocity) * chassis.WheelRadii[3]) * 0.5f;
            return Mathf.Min(frontSpeed, rearSpeed);
        }

        private float GetDrivenWheelsAverageAngularVelocity(in VehicleState state)
        {
            switch (Drivetrain)
            {
                case DrivetrainType.FrontWheelDrive:
                    return (state.Wheels[0].AngularVelocity + state.Wheels[1].AngularVelocity) * 0.5f;
                case DrivetrainType.RearWheelDrive:
                    return (state.Wheels[2].AngularVelocity + state.Wheels[3].AngularVelocity) * 0.5f;
                case DrivetrainType.AllWheelDrive:
                default:
                    return (state.Wheels[0].AngularVelocity + state.Wheels[1].AngularVelocity +
                            state.Wheels[2].AngularVelocity + state.Wheels[3].AngularVelocity) * 0.25f;
            }
        }
    }
}
