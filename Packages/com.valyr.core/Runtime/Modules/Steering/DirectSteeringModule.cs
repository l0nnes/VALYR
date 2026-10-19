using System;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Interfaces;
using VALYR.Core.Modules;

namespace VALYR.Modules.Steering
{
    /// <summary>
    /// Front-wheel steering module featuring speed-sensitive steering angle attenuation
    /// and kinematic Ackermann steering geometry for optimal inside vs outside wheel turning radii.
    /// </summary>
    [Serializable]
    public class DirectSteeringModule : VehicleModule, ISteeringModule
    {
        [Header("Steer Angle Limits")]
        [Tooltip("Maximum front wheel steer angle at low / parking speeds in degrees (typically 30 - 40 deg).")]
        [Range(15f, 50f)]
        public float MaxSteerAngleDeg = 36f;

        [Header("Speed Sensitivity")]
        [Tooltip("Automatically attenuates the maximum steering angle as vehicle speed rises to maintain highway directional stability.")]
        public bool EnableSpeedSensitivity = true;

        [Tooltip("Maximum allowable steer angle at high speed in degrees (typically 10 - 15 deg).")]
        [Range(5f, 25f)]
        public float MinSteerAngleAtHighSpeedDeg = 12f;

        [Tooltip("Vehicle linear forward speed in m/s at which steering attenuation reaches its minimum limit (e.g. 40 m/s = 144 km/h).")]
        [Range(10f, 100f)]
        public float HighSpeedThreshold = 40f;

        [Header("Steering Dynamics & Geometry")]
        [Tooltip("Rack response rate in 1/s (speed of steering servo / rack smoothing). Higher values provide sharper steering response.")]
        [Range(4f, 30f)]
        public float SteeringResponseRate = 16f;

        [Tooltip("Enable Ackermann steering geometry so the inside wheel steers sharper than the outside wheel, preventing tire scrubbing in sharp turns.")]
        public bool UseAckermann = true;

        public DirectSteeringModule()
        {
            ModuleId = "Steering.Direct";
        }

        public void UpdateSteering(
            in VehicleInput input,
            ref VehicleState state,
            IPhysicsBody body,
            in VehicleChassisConfig chassis,
            float dt)
        {
            if (!IsEnabled)
                return;

            float steerInput = Mathf.Clamp(input.Steer, -1f, 1f);

            // 1. Evaluate speed-sensitive effective maximum steering angle
            float forwardSpeed = Vector3.Dot(body.LinearVelocity, body.Rotation * Vector3.forward);
            float speedAbs = Mathf.Abs(forwardSpeed);

            float currentMaxAngleDeg = MaxSteerAngleDeg;
            if (EnableSpeedSensitivity && HighSpeedThreshold > 0.01f)
            {
                float speedFactor = Mathf.Clamp01(speedAbs / HighSpeedThreshold);
                // Smooth quadratic falloff
                currentMaxAngleDeg = Mathf.Lerp(MaxSteerAngleDeg, MinSteerAngleAtHighSpeedDeg, speedFactor * (2f - speedFactor));
            }

            // Target steer angle in radians
            float targetSteerRad = (steerInput * currentMaxAngleDeg) * Mathf.Deg2Rad;

            // Rack servo response smoothing
            float currentSteerRad = (state.Wheels[0].SteerAngle + state.Wheels[1].SteerAngle) * 0.5f;
            float smoothSteerRad = Mathf.Lerp(currentSteerRad, targetSteerRad, 1f - Mathf.Exp(-SteeringResponseRate * dt));

            float steerAngleFL;
            float steerAngleFR;

            // 2. Ackermann steering geometry
            if (UseAckermann && Mathf.Abs(smoothSteerRad) > 0.001f)
            {
                float wheelbase = Mathf.Abs(chassis.WheelOffsets[0].z - chassis.WheelOffsets[2].z);
                float trackWidth = Mathf.Abs(chassis.WheelOffsets[1].x - chassis.WheelOffsets[0].x);

                if (wheelbase > 0.1f && trackWidth > 0.1f)
                {
                    float turnRadius = wheelbase / Mathf.Tan(Mathf.Abs(smoothSteerRad));

                    if (smoothSteerRad > 0f) // Steer right: FR is inside, FL is outside
                    {
                        steerAngleFR = Mathf.Atan(wheelbase / (turnRadius - trackWidth * 0.5f));
                        steerAngleFL = Mathf.Atan(wheelbase / (turnRadius + trackWidth * 0.5f));
                    }
                    else // Steer left: FL is inside, FR is outside
                    {
                        steerAngleFL = -Mathf.Atan(wheelbase / (turnRadius - trackWidth * 0.5f));
                        steerAngleFR = -Mathf.Atan(wheelbase / (turnRadius + trackWidth * 0.5f));
                    }
                }
                else
                {
                    steerAngleFL = smoothSteerRad;
                    steerAngleFR = smoothSteerRad;
                }
            }
            else
            {
                // Parallel steering
                steerAngleFL = smoothSteerRad;
                steerAngleFR = smoothSteerRad;
            }

            // Assign to front wheels (FL, FR)
            state.Wheels.GetRef(0).SteerAngle = steerAngleFL;
            state.Wheels.GetRef(1).SteerAngle = steerAngleFR;

            // Rear wheels remain fixed
            state.Wheels.GetRef(2).SteerAngle = 0f;
            state.Wheels.GetRef(3).SteerAngle = 0f;
        }
    }
}
