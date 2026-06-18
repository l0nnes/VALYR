using System;
using Core.Attributes;
using Core.Configs;
using Core.Contexts;
using Core.Interfaces;
using Core.Math;

namespace Core.Modules
{
    [Serializable]
    public class DrivetrainModule : IVehicleModule
    {
        private Config _config = new();

        public VehicleModulePhase Phase => VehicleModulePhase.Differential;

        public void SetConfiguration(ModuleConfig config)
        {
            if (config is Config typed) _config = typed;
        }

        public void Initialize(VehicleContext context)
        {
            UpdatePoweredAxleFlags(context);
        }

        public void Update(VehicleContext context, float dt)
        {
            UpdatePoweredAxleFlags(context);

            foreach (var wheel in context.wheels) wheel.state.driveTorque = 0f;

            var totalTorque = context.transmission.outputTorque;
            if (MathUtil.Abs(totalTorque) < 0.001f || context.axles.Count == 0) return;

            switch (_config.driveLayout)
            {
                case DriveLayout.FWD:
                    ApplyAxleTorque(context.axles[0], totalTorque, GetAxleLockMode(context.axles[0]));
                    break;

                case DriveLayout.RWD:
                    ApplyAxleTorque(context.axles[context.axles.Count - 1], totalTorque,
                        GetAxleLockMode(context.axles[context.axles.Count - 1]));
                    break;

                case DriveLayout.AWD:
                    ApplyAwdTorque(context, totalTorque);
                    break;

                case DriveLayout.CustomAxles:
                    ApplyCustomAxleTorque(context, totalTorque);
                    break;
            }
        }

        private void ApplyAwdTorque(VehicleContext context, float totalTorque)
        {
            if (context.axles.Count == 1)
            {
                ApplyAxleTorque(context.axles[0], totalTorque, GetAxleLockMode(context.axles[0]));
                return;
            }

            var front = context.axles[0];
            var rear = context.axles[context.axles.Count - 1];

            var frontShare = MathUtil.Clamp01(_config.awdFrontTorqueShare);
            var frontTorque = totalTorque * frontShare;
            var rearTorque = totalTorque - frontTorque;

            ApplyCenterLockCorrection(front, rear, ref frontTorque, ref rearTorque);

            ApplyAxleTorque(front, frontTorque, GetAxleLockMode(front));
            ApplyAxleTorque(rear, rearTorque, GetAxleLockMode(rear));
        }

        private void ApplyCustomAxleTorque(VehicleContext context, float totalTorque)
        {
            var poweredCount = 0;
            foreach (var axle in context.axles)
                if (axle.config.isPowered)
                    poweredCount++;

            if (poweredCount == 0) return;

            var torquePerAxle = totalTorque / poweredCount;
            foreach (var axle in context.axles)
            {
                if (!axle.config.isPowered) continue;
                ApplyAxleTorque(axle, torquePerAxle, GetAxleLockMode(axle));
            }
        }

        private void ApplyAxleTorque(AxleContext axle, float axleTorque, DifferentialLockMode lockMode)
        {
            var leftTorque = axleTorque * 0.5f;
            var rightTorque = axleTorque * 0.5f;

            ApplyWheelLockCorrection(axle, lockMode, ref leftTorque, ref rightTorque);

            axle.leftWheel.state.driveTorque += leftTorque;
            axle.rightWheel.state.driveTorque += rightTorque;
        }

        private void ApplyCenterLockCorrection(AxleContext front, AxleContext rear, ref float frontTorque,
            ref float rearTorque)
        {
            if (_config.centerLock == DifferentialLockMode.Open) return;

            var frontOmega = GetAverageAxleOmega(front);
            var rearOmega = GetAverageAxleOmega(rear);
            var correction = (rearOmega - frontOmega) * _config.lockStrength;
            var maxCorrection = MathUtil.Abs(frontTorque + rearTorque) * GetCorrectionShare(_config.centerLock);

            correction = MathUtil.Clamp(correction, -maxCorrection, maxCorrection);
            frontTorque += correction;
            rearTorque -= correction;
        }

        private void ApplyWheelLockCorrection(AxleContext axle, DifferentialLockMode lockMode, ref float leftTorque,
            ref float rightTorque)
        {
            if (lockMode == DifferentialLockMode.Open) return;

            var delta = axle.rightWheel.state.tire.angularVelocity - axle.leftWheel.state.tire.angularVelocity;
            var correction = delta * _config.lockStrength;
            var maxCorrection = MathUtil.Abs(leftTorque + rightTorque) * GetCorrectionShare(lockMode);

            correction = MathUtil.Clamp(correction, -maxCorrection, maxCorrection);
            leftTorque += correction;
            rightTorque -= correction;
        }

        private float GetCorrectionShare(DifferentialLockMode lockMode)
        {
            return lockMode == DifferentialLockMode.Locked
                ? MathUtil.Clamp01(_config.lockedCorrectionShare)
                : MathUtil.Clamp01((_config.lsdTorqueBias - 1f) / MathUtil.Max(_config.lsdTorqueBias + 1f, 0.001f));
        }

        private DifferentialLockMode GetAxleLockMode(AxleContext axle)
        {
            if (axle.index == 0) return _config.frontAxleLock;
            if (axle.index == _config.rearAxleIndex || axle.index == axle.vehicle.axles.Count - 1)
                return _config.rearAxleLock;
            return _config.middleAxleLock;
        }

        private void UpdatePoweredAxleFlags(VehicleContext context)
        {
            for (var i = 0; i < context.axles.Count; i++) context.axles[i].config.isPowered = IsPoweredAxle(context, i);
        }

        private bool IsPoweredAxle(VehicleContext context, int axleIndex)
        {
            return _config.driveLayout switch
            {
                DriveLayout.FWD => axleIndex == 0,
                DriveLayout.RWD => axleIndex == context.axles.Count - 1,
                DriveLayout.AWD => axleIndex == 0 || axleIndex == context.axles.Count - 1,
                DriveLayout.CustomAxles => context.axles[axleIndex].config.isPowered,
                _ => false
            };
        }

        private static float GetAverageAxleOmega(AxleContext axle)
        {
            return (axle.leftWheel.state.tire.angularVelocity + axle.rightWheel.state.tire.angularVelocity) * 0.5f;
        }

        [Serializable]
        public class Config : ModuleConfig
        {
            [ConfigHeader("Drive Layout")] public DriveLayout driveLayout = DriveLayout.RWD;

            public float awdFrontTorqueShare = 0.4f;
            public int rearAxleIndex = 1;

            [ConfigHeader("Differential Locks")] public DifferentialLockMode centerLock = DifferentialLockMode.Open;

            public DifferentialLockMode frontAxleLock = DifferentialLockMode.Open;
            public DifferentialLockMode rearAxleLock = DifferentialLockMode.Open;
            public DifferentialLockMode middleAxleLock = DifferentialLockMode.Open;

            [ConfigHeader("Lock Tuning")] public float lockStrength = 18f;

            public float lockedCorrectionShare = 0.45f;
            public float lsdTorqueBias = 2.5f;
        }
    }

    public enum DriveLayout
    {
        FWD,
        RWD,
        AWD,
        CustomAxles
    }

    public enum DifferentialLockMode
    {
        Open,
        LimitedSlip,
        Locked
    }
}