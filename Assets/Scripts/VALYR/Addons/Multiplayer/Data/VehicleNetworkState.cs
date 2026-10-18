using System;
using PurrNet.Packing;

namespace VALYR.Multiplayer
{
    /// <summary>
    /// Network-replicated driving and wheel state for VALYR vehicles.
    /// Serialized automatically using PurrNet's IPackedAuto bit-packing.
    /// Zero allocations and highly optimized wire bandwidth.
    /// </summary>
    [Serializable]
    public struct VehicleNetworkState : IPackedAuto
    {
        public float EngineRpm;
        public int CurrentGear;
        public float SteerAngle;
        public float ForwardSpeed;

        // Suspension lengths for 4 wheels (FL, FR, RL, RR)
        public float SuspLength0;
        public float SuspLength1;
        public float SuspLength2;
        public float SuspLength3;

        // Visual cues & driving states
        public bool IsBraking;
        public bool IsHandbraking;
        public bool IsReversing;
    }
}
