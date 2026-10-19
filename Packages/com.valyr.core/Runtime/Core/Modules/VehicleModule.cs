using System;
using UnityEngine;

namespace VALYR.Core.Modules
{
    /// <summary>
    /// Geometric configuration and placement of chassis suspension mount points and wheels.
    /// </summary>
    [Serializable]
    public struct VehicleChassisConfig
    {
        [Tooltip("Local suspension top anchor / mount points for the 4 wheels (FL, FR, RL, RR) relative to vehicle body center (meters).")]
        public WheelData4<Vector3> WheelOffsets;

        [Tooltip("Unloaded outer rolling radius of the 4 tires in meters (e.g. 0.31m for 185/65 R15, 0.34m for 225/45 R17).")]
        public WheelData4<float> WheelRadii;

        [Tooltip("Total curb mass of the vehicle chassis in kilograms (excluding wheel inertia).")]
        [Min(100f)]
        public float ChassisMass;

        public static VehicleChassisConfig Default => new()
        {
            WheelOffsets = new WheelData4<Vector3>(
                new Vector3(-0.70f, 0.655f, 1.25f),  // FL
                new Vector3(0.70f, 0.655f, 1.25f),   // FR
                new Vector3(-0.70f, 0.655f, -1.20f), // RL
                new Vector3(0.70f, 0.655f, -1.20f)   // RR
            ),
            WheelRadii = new WheelData4<float>(0.31f, 0.31f, 0.31f, 0.31f),
            ChassisMass = 1180f
        };
    }

    /// <summary>
    /// Base class for all modular vehicle subsystems (Suspension, Steering, Powertrain, Tires, Assists).
    /// </summary>
    [Serializable]
    public abstract class VehicleModule
    {
        [HideInInspector]
        public string ModuleId;

        [HideInInspector]
        public bool IsEnabled = true;

        /// <summary>
        /// Initializes the module with vehicle chassis configuration.
        /// </summary>
        public virtual void Initialize(in VehicleChassisConfig chassis) { }

        /// <summary>
        /// Resets internal state (e.g. on teleport, reset, or scene change).
        /// </summary>
        public virtual void Reset() { }
    }
}
