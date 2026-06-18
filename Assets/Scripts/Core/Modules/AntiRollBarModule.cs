using System;
using Core.Attributes;
using Core.Configs;
using Core.Contexts;
using Core.Interfaces;
using Core.Math;

namespace Core.Modules
{
    [Serializable]
    public class AntiRollBarModule : IVehicleModule
    {
        private Config _config = new();

        public VehicleModulePhase Phase => VehicleModulePhase.AntiRollBar;

        public void SetConfiguration(ModuleConfig config)
        {
            if (config is Config typed) _config = typed;
        }

        public void Initialize(VehicleContext context)
        {
        }

        public void Update(VehicleContext context, float dt)
        {
            foreach (var axle in context.axles)
            {
                var leftCompression = axle.leftWheel.state.suspension.compressionRatio;
                var rightCompression = axle.rightWheel.state.suspension.compressionRatio;
                var force = (leftCompression - rightCompression) * _config.stiffness;

                axle.state.antiRollForce = force;

                if (axle.leftWheel.state.hit.isGrounded)
                {
                    axle.leftWheel.state.hit.normalForce =
                        MathUtil.Max(0f, axle.leftWheel.state.hit.normalForce + force);
                    axle.leftWheel.state.suspension.force =
                        MathUtil.Max(0f, axle.leftWheel.state.suspension.force + force);
                }

                if (axle.rightWheel.state.hit.isGrounded)
                {
                    axle.rightWheel.state.hit.normalForce =
                        MathUtil.Max(0f, axle.rightWheel.state.hit.normalForce - force);
                    axle.rightWheel.state.suspension.force =
                        MathUtil.Max(0f, axle.rightWheel.state.suspension.force - force);
                }
            }
        }

        [Serializable]
        public class Config : ModuleConfig
        {
            [ConfigHeader("Anti-Roll")] public float stiffness = 4500f;
        }
    }
}