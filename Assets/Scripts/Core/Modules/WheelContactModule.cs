using System;
using Core.Attributes;
using Core.Configs;
using Core.Contexts;
using Core.Interfaces;
using Core.Math;

namespace Core.Modules
{
    [Serializable]
    public class WheelContactModule : IVehicleModule
    {
        private const float DefaultCastDistance = 0.5f;
        private const float DefaultTireStiffness = 45000f;
        private const float DefaultMaxNormalForce = 25000f;
        private const float DefaultSectorAngle = 160f;
        private const int DefaultRadialResolution = 9;
        private const int DefaultWidthResolution = 5;
        private const float DefaultWidthFactor = 0.95f;

        private Config _config = new();

        public VehicleModulePhase Phase => VehicleModulePhase.GroundDetection;

        public void SetConfiguration(ModuleConfig config)
        {
            if (config is Config typed) _config = typed;
        }

        public void Initialize(VehicleContext context)
        {
        }

        public void Update(VehicleContext context, float dt)
        {
            var bodyPos = context.body.position;
            var bodyRot = context.body.rotation;
            var bodyUp = bodyRot * Vec3.Up;

            foreach (var wheel in context.wheels)
            {
                UpdateWheelGeometry(wheel, bodyPos, bodyRot, bodyUp);
                ScanContactPatch(context.host, wheel);
            }
        }

        private static void UpdateWheelGeometry(WheelContext wheel, Vec3 bodyPos, Quat bodyRot, Vec3 bodyUp)
        {
            wheel.state.worldHardPoint = bodyPos + bodyRot * wheel.state.localHardPoint;
            wheel.state.upDir = bodyUp;

            var length = wheel.state.suspension.currentLength > 0f
                ? wheel.state.suspension.currentLength
                : wheel.state.suspension.restLength;
            wheel.state.worldPosition = wheel.state.worldHardPoint - bodyUp * length;

            var steerRot = Quat.Euler(0f, wheel.state.steeringAngle, 0f);
            wheel.state.worldRotation = bodyRot * steerRot;

            wheel.state.forwardDir = wheel.state.worldRotation * Vec3.Forward;
            wheel.state.rightDir = wheel.state.worldRotation * Vec3.Right;
        }

        private void ScanContactPatch(IVehicleHost host, WheelContext wheel)
        {
            var radius = wheel.wheelConfig.radius;
            var suspensionLength = wheel.state.suspension.restLength > 0f
                ? wheel.state.suspension.restLength
                : _config.castDistance > 0f
                    ? _config.castDistance
                    : DefaultCastDistance;
            var maxRange = radius + suspensionLength;
            var radialResolution = _config.radialResolution > 0 ? _config.radialResolution : DefaultRadialResolution;
            var widthResolution = _config.widthResolution > 0 ? _config.widthResolution : DefaultWidthResolution;

            var widthFactor = _config.widthFactor > 0f ? _config.widthFactor : DefaultWidthFactor;
            var effectiveWidth = wheel.wheelConfig.width * widthFactor;
            var widthStep = widthResolution > 1 ? effectiveWidth / (widthResolution - 1) : 0f;
            var widthStart = -effectiveWidth * 0.5f;

            var sectorAngle = _config.sectorAngle > 0f ? _config.sectorAngle : DefaultSectorAngle;
            var sectorRad = sectorAngle * MathUtil.Deg2Rad;
            var angleStep = radialResolution > 1 ? sectorRad / (radialResolution - 1) : 0f;
            var angleStart = -sectorRad * 0.5f;

            var hardPoint = wheel.state.worldHardPoint;
            var down = -wheel.state.upDir;
            var forward = wheel.state.forwardDir;
            var right = wheel.state.rightDir;

            var weightedPoint = Vec3.Zero;
            var weightedNormal = Vec3.Zero;
            var totalWeight = 0f;
            var penetrationSum = 0f;
            var maxPenetration = 0f;
            var minSuspensionLength = float.MaxValue;
            var hitCount = 0;
            var surfaceVelocity = Vec3.Zero;

            for (var w = 0; w < widthResolution; w++)
            {
                var widthOffset = widthStart + widthStep * w;

                for (var r = 0; r < radialResolution; r++)
                {
                    var angle = angleStart + angleStep * r;
                    var sin = (float)System.Math.Sin(angle);
                    var cos = (float)System.Math.Cos(angle);
                    var rayDir = (down * cos + forward * sin).Normalize();
                    var sampleOrigin = hardPoint + right * widthOffset + rayDir * radius;

                    if (!host.Raycast(sampleOrigin, down, suspensionLength, out var result)) continue;

                    var penetration = MathUtil.Max(0f, suspensionLength - result.Distance);
                    if (penetration <= 0f) continue;

                    var weight = penetration;
                    weightedPoint += result.Point * weight;
                    weightedNormal += result.Normal * weight;
                    surfaceVelocity += result.SurfaceVelocity * weight;
                    totalWeight += weight;
                    penetrationSum += penetration;
                    maxPenetration = MathUtil.Max(maxPenetration, penetration);
                    minSuspensionLength = MathUtil.Min(minSuspensionLength, result.Distance);
                    hitCount++;
                }
            }

            if (totalWeight <= MathUtil.Epsilon)
            {
                ResetHit(wheel, hardPoint, down, maxRange);
                return;
            }

            var normal = (weightedNormal / totalWeight).Normalize();
            if (normal.SqrMagnitude() < MathUtil.Epsilon) normal = wheel.state.upDir;

            var contactForward = Vec3.ProjectOnPlane(wheel.state.forwardDir, normal).Normalize();
            if (contactForward.SqrMagnitude() < MathUtil.Epsilon)
                contactForward = Vec3.ProjectOnPlane(Vec3.Forward, normal).Normalize();

            var contactRight = Vec3.Cross(normal, contactForward).Normalize();
            if (contactRight.SqrMagnitude() < MathUtil.Epsilon)
                contactRight = Vec3.ProjectOnPlane(wheel.state.rightDir, normal).Normalize();

            var hit = wheel.state.hit;
            hit.isGrounded = true;
            hit.point = weightedPoint / totalWeight;
            hit.normal = normal;
            hit.distance = minSuspensionLength + radius;
            hit.surfaceVelocity = surfaceVelocity / totalWeight;
            hit.penetration = maxPenetration;
            hit.penetrationSum = penetrationSum;
            var averagePenetration = hitCount > 0 ? penetrationSum / hitCount : 0f;
            var tireStiffness = _config.tireStiffness > 0f ? _config.tireStiffness : DefaultTireStiffness;
            var maxNormalForce = _config.maxNormalForce > 0f ? _config.maxNormalForce : DefaultMaxNormalForce;
            hit.normalForce = MathUtil.Min(averagePenetration * tireStiffness, maxNormalForce);
            hit.contactForward = contactForward;
            hit.contactRight = contactRight;
        }

        private static void ResetHit(WheelContext wheel, Vec3 hardPoint, Vec3 down, float maxRange)
        {
            var hit = wheel.state.hit;
            hit.isGrounded = false;
            hit.point = hardPoint + down * maxRange;
            hit.normal = wheel.state.upDir;
            hit.distance = maxRange;
            hit.surfaceVelocity = Vec3.Zero;
            hit.penetration = 0f;
            hit.penetrationSum = 0f;
            hit.normalForce = 0f;
            hit.contactForward = wheel.state.forwardDir;
            hit.contactRight = wheel.state.rightDir;
        }

        [Serializable]
        public class Config : ModuleConfig
        {
            [ConfigHeader("Soft Tire Contact")]
            [ConfigTooltip(
                "Fallback maximum suspension length in meters. If SuspensionModule is present, its restLength is used instead.")]
            public float castDistance = DefaultCastDistance;

            public float tireStiffness = DefaultTireStiffness;
            public float maxNormalForce = DefaultMaxNormalForce;

            [ConfigHeader("Ray Array")] [ConfigRange(20f, 180f)]
            public float sectorAngle = DefaultSectorAngle;

            [ConfigRange(1, 32)] public int radialResolution = DefaultRadialResolution;
            [ConfigRange(1, 12)] public int widthResolution = DefaultWidthResolution;
            [ConfigRange(0.1f, 1f)] public float widthFactor = DefaultWidthFactor;
        }
    }
}