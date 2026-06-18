using System;
using System.Collections.Generic;
using Core.Contexts;
using Core.Interfaces;
using Core.Math;

namespace Core
{
    public class VehicleCore
    {
        private readonly List<IVehicleModule> _macroModules = new();
        private readonly List<IVehicleModule> _microModules = new();

        public VehicleCore(VehicleConfig config)
        {
            Context = new VehicleContext
            {
                config = config,
                body = new RigidbodyState(),
                input = new InputState()
            };

            var allModules = new List<IVehicleModule>(config.modules ?? Array.Empty<IVehicleModule>());
            allModules.Sort((a, b) => a.Phase.CompareTo(b.Phase));

            foreach (var module in allModules)
                if (module.Phase == VehicleModulePhase.Tire)
                    _microModules.Add(module);
                else
                    _macroModules.Add(module);
        }

        public VehicleContext Context { get; }

        public void Initialize(IVehicleHost host, Vec3[] hardPoints)
        {
            Context.host = host;
            BuildContext(hardPoints);

            foreach (var module in _macroModules) module.Initialize(Context);
            foreach (var module in _microModules) module.Initialize(Context);
        }

        public void Simulate()
        {
            if (Context.host == null) return;
            var dt = Context.host.DeltaTime;

            var subSteps = Context.config.subSteps;
            var subDt = dt / subSteps;

            Context.input = Context.host.GetInput();
            Context.RecalculateBody();

            foreach (var module in _macroModules) module.Update(Context, dt);

            for (var i = 0; i < subSteps; i++)
            {
                foreach (var module in _microModules) module.Update(Context, subDt);

                ApplyTireForces(1f / subSteps);
                ApplyWheelNormalForces(1f / subSteps);
            }
        }

        private void BuildContext(Vec3[] hardPoints)
        {
            var config = Context.config;
            for (var i = 0; i < config.axles.Length; i++)
            {
                var axleConfig = config.axles[i];
                var axleCtx = new AxleContext
                {
                    vehicle = Context,
                    config = axleConfig,
                    state = new AxleState(),
                    index = i
                };

                axleCtx.leftWheel = CreateWheel(axleCtx, i * 2, true, hardPoints);
                axleCtx.rightWheel = CreateWheel(axleCtx, i * 2 + 1, false, hardPoints);

                Context.axles.Add(axleCtx);
                Context.wheels.Add(axleCtx.leftWheel);
                Context.wheels.Add(axleCtx.rightWheel);
            }
        }

        private WheelContext CreateWheel(AxleContext axle, int index, bool isLeft, Vec3[] hardPoints)
        {
            var localPos = index < hardPoints.Length ? hardPoints[index] : Vec3.Zero;
            return new WheelContext
            {
                axle = axle,
                index = index,
                isLeft = isLeft,
                wheelConfig = axle.config.wheel,
                state = new WheelState { localHardPoint = localPos }
            };
        }

        private void ApplyTireForces(float scale)
        {
            foreach (var wheel in Context.wheels)
            {
                if (!wheel.state.hit.isGrounded) continue;

                var tireTotalForce = wheel.state.tire.totalForce;
                Context.host.ApplyForce(tireTotalForce * scale, wheel.state.hit.point);
            }
        }

        private void ApplyWheelNormalForces(float scale)
        {
            foreach (var wheel in Context.wheels)
            {
                if (!wheel.state.hit.isGrounded) continue;

                var hasSoftContactNormalForce = wheel.state.hit.normalForce > MathUtil.Epsilon;
                var normal = hasSoftContactNormalForce && wheel.state.hit.normal.SqrMagnitude() > MathUtil.Epsilon
                    ? wheel.state.hit.normal.Normalize()
                    : wheel.state.upDir;

                var forcePoint = hasSoftContactNormalForce
                    ? wheel.state.hit.point
                    : wheel.state.worldPosition;

                var normalForce = hasSoftContactNormalForce
                    ? MathUtil.Max(MathUtil.Max(0f, wheel.state.hit.normalForce),
                        MathUtil.Max(0f, wheel.state.suspension.force))
                    : MathUtil.Max(0f, wheel.state.suspension.force);

                Context.host.ApplyForce(normal * normalForce * scale, forcePoint);
            }
        }
    }
}