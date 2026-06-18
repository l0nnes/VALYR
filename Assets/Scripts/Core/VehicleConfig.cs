using System;
using Core.Interfaces;
using Core.Math;

namespace Core
{
    [Serializable]
    public class VehicleConfig
    {
        public int subSteps = 4;
        public float mass;
        public Vec3 centerOfMass;
        public Vec3 inertiaTensorScale = Vec3.One;
        public AxleConfig[] axles;
        public IVehicleModule[] modules;
    }

    [Serializable]
    public class AxleConfig
    {
        public bool isPowered;
        public SteeringMode steeringMode;

        public WheelConfig wheel;
    }

    [Serializable]
    public class WheelConfig
    {
        public float radius;
        public float width;
        public float mass;
        public float inertia;
    }

    public enum SteeringMode
    {
        Disable,
        Standard,
        Reverse
    }
}