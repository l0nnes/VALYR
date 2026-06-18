using System;
using Core.Math;

namespace Core
{
    [Serializable]
    public class AxleState
    {
        public float steerAngle;
        public float antiRollForce;
    }

    [Serializable]
    public class EngineState
    {
        public float rpm;
        public float angularVelocity; // Flywheel angular velocity (rad/s)
        public float torque; // Combustion torque for UI and drivetrain
        public float loadTorque; // Opposing clutch/drivetrain torque
        public float inertia; // Engine inertia used by transmission
        public float powerKw;
        public float normalizedRpm;
        public bool inCutoff;
        public float crankTimer;
        public bool isRunning = true;
    }

    [Serializable]
    public class ClutchState
    {
        public float engagement; // 0 disengaged, 1 engaged
    }

    [Serializable]
    public class TransmissionState
    {
        public int currentGear = 1;
        public float currentGearRatio;
        public float torque;
        public float outputTorque;
        public float inputShaftRpm;
        public float outputShaftRpm;
        public float clutchSlip;
        public float shiftTimer;
        public float brakeReverseTimer;
        public bool isAutomatic;
        public bool isShifting;
        public bool isAutoHoldActive;
        public bool isBrakeReverseActive;
    }

    [Serializable]
    public class WheelState
    {
        public Vec3 localHardPoint;
        public Vec3 worldHardPoint;
        public Vec3 worldPosition;
        public Quat worldRotation;
        public Vec3 forwardDir;
        public Vec3 rightDir;
        public Vec3 upDir;

        public float driveTorque;
        public float brakeTorque;

        public float steeringAngle;

        public GroundHitState hit = new();
        public SuspensionState suspension = new();
        public TireState tire = new();
    }

    [Serializable]
    public struct RigidbodyState
    {
        public Vec3 position;
        public Quat rotation;
        public Vec3 linearVelocity;
        public Vec3 angularVelocity;
    }

    [Serializable]
    public struct InputState
    {
        public float throttle;
        public float steering;
        public float brake;
        public bool isUpshift;
        public bool isDownshift;
        public bool handbrake;
        public float clutch;
    }

    [Serializable]
    public class GroundHitState
    {
        public bool isGrounded;
        public Vec3 point;
        public Vec3 normal;
        public float distance;
        public Vec3 surfaceVelocity;
        public float penetration;
        public float penetrationSum;
        public float normalForce;
        public Vec3 contactForward;
        public Vec3 contactRight;
    }

    [Serializable]
    public class SuspensionState
    {
        public float restLength;
        public float compressionRatio;
        public float currentLength;
        public float force;
    }

    [Serializable]
    public class TireState
    {
        public float longForce;
        public float latForce;
        public Vec3 totalForce;
        public float angularVelocity;
        public float angularAcceleration;
        public float slipAngle;
        public float slipRatio;
        public float normalForce;
        public float frictionUsage;
        public float rollingResistanceTorque;
        public float visualAngularVelocity;
    }
}