using System;
using UnityEngine;

namespace VALYR.Core
{
    /// <summary>
    /// Complete snapshot of the vehicle physics state for a single simulation tick.
    /// Blittable unmanaged struct with zero heap allocations, designed for rollback replays
    /// and network prediction (compatible with PurrDiction IPredictedData).
    /// </summary>
    [Serializable]
    public struct VehicleState
    {
        public uint Tick;

        // --- Powertrain State ---
        [Tooltip("Engine crankshaft rotational speed in RPM.")]
        public float EngineRpm;

        [Tooltip("Currently engaged transmission gear (-1: Reverse, 0: Neutral, 1..N: Forward gears).")]
        public int CurrentGear;

        [Tooltip("Clutch lockup factor (0 = fully disengaged / slipping, 1 = fully locked).")]
        public float ClutchEngagement;

        [Tooltip("Active gear shift timer in seconds.")]
        public float ShiftTimer;

        // --- 4 Wheels Physical State ---
        public WheelData4<WheelState> Wheels;

        public void Dispose() { }
    }
}
