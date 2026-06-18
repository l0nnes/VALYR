using System.Collections.Generic;
using Core.Interfaces;

namespace Core.Contexts
{
    public class VehicleContext
    {
        public readonly List<AxleContext> axles = new();
        public readonly List<WheelContext> wheels = new();
        public RigidbodyState body;
        public ClutchState clutch = new();
        public VehicleConfig config;
        public EngineState engine = new();
        public IVehicleHost host;

        public InputState input;
        public TransmissionState transmission = new();

        public void RecalculateBody()
        {
            body.position = host.GetPosition();
            body.rotation = host.GetRotation();
            body.linearVelocity = host.GetLinearVelocity();
            body.angularVelocity = host.GetAngularVelocity();
        }
    }

    public class AxleContext
    {
        public AxleConfig config;

        public int index;

        public WheelContext leftWheel;
        public WheelContext rightWheel;
        public AxleState state;
        public VehicleContext vehicle;
    }

    public class WheelContext
    {
        public AxleContext axle;

        public int index;
        public bool isLeft;

        public WheelState state;

        public WheelConfig wheelConfig;
        public VehicleContext Vehicle => axle.vehicle;
    }
}