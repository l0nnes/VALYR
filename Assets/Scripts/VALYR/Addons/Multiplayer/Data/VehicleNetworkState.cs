using System;
using PurrNet.Packing;

namespace VALYR.Multiplayer
{
    /// <summary>
    /// Network-replicated driving and visual state for VALYR vehicles.
    /// Serialized automatically using PurrNet's IPackedAuto bit-packing.
    /// Zero allocations and highly optimized wire bandwidth.
    /// </summary>
    [Serializable]
    public struct VehicleNetworkState : IPackedAuto
    {
        public float EngineRpm;
        public int CurrentGear;
        public float SteerAngle;

        // Suspension lengths for 4 wheels (FL, FR, RL, RR)
        public float SuspLength0;
        public float SuspLength1;
        public float SuspLength2;
        public float SuspLength3;

        // Accumulated rolling rotation angles in radians for 4 wheels
        public float RollAngle0;
        public float RollAngle1;
        public float RollAngle2;
        public float RollAngle3;

        // Visual cues & driving states
        public bool IsBraking;
        public bool IsHandbraking;
        public bool IsReversing;

        // Lighting & Signals
        public byte HeadlightMode; // 0 = Off, 1 = LowBeam, 2 = HighBeam
        public bool IsHazardActive;
        public bool IsLeftTurnActive;
        public bool IsRightTurnActive;

        // Interactive Doors bitmask (bit 0=LeftDoor, bit 1=RightDoor, bit 2=Hood, bit 3=Tailgate)
        public byte DoorOpenFlags;
    }
}
