using System;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Interfaces;
using VALYR.Core.Modules;

namespace VALYR.Modules.Assists
{
    /// <summary>
    /// Anti-Roll Bar (Sway Bar) chassis assist module.
    /// Resists chassis body roll during cornering by transferring normal load between
    /// the left and right wheels of each axle.
    /// </summary>
    [Serializable]
    public class AntiRollBarModule : VehicleModule, IChassisAssistModule
    {
        [Header("Anti-Roll Bar Stiffness")]
        [Tooltip("Front anti-roll bar torsional stiffness in Newtons per meter (N/m, typical: 3,000 - 8,000 N/m). Higher values reduce roll and promote understeer.")]
        [Min(0f)]
        public float FrontStiffness = 4500f;

        [Tooltip("Rear anti-roll bar torsional stiffness in Newtons per meter (N/m, typical: 1,500 - 5,000 N/m). Higher values reduce roll and promote oversteer.")]
        [Min(0f)]
        public float RearStiffness = 2200f;

        public AntiRollBarModule()
        {
            ModuleId = "Assists.AntiRollBar";
        }

        public void ApplyChassisAssists(
            ref VehicleState state,
            IPhysicsBody body,
            in VehicleChassisConfig chassis,
            float dt)
        {
            if (!IsEnabled)
                return;

            Vector3 bodyUp = body.Rotation * Vector3.up;

            // 1. Front Axle Anti-Roll Bar (FL = 0, FR = 1)
            ApplyAxleAntiRoll(
                ref state.Wheels.GetRef(0),
                ref state.Wheels.GetRef(1),
                FrontStiffness,
                body,
                bodyUp);

            // 2. Rear Axle Anti-Roll Bar (RL = 2, RR = 3)
            ApplyAxleAntiRoll(
                ref state.Wheels.GetRef(2),
                ref state.Wheels.GetRef(3),
                RearStiffness,
                body,
                bodyUp);
        }

        private static void ApplyAxleAntiRoll(
            ref WheelState leftWheel,
            ref WheelState rightWheel,
            float stiffness,
            IPhysicsBody body,
            Vector3 bodyUp)
        {
            if (stiffness <= 0f)
                return;

            float leftComp = leftWheel.HasContact ? leftWheel.SuspensionCompression : 0f;
            float rightComp = rightWheel.HasContact ? rightWheel.SuspensionCompression : 0f;

            float diff = leftComp - rightComp;
            if (Mathf.Abs(diff) < 0.001f)
                return;

            float deltaForce = diff * stiffness;

            // Load transfer: compressed wheel gains normal force, decompressed wheel loses normal force
            if (leftWheel.HasContact)
            {
                leftWheel.NormalLoad = Mathf.Max(0f, leftWheel.NormalLoad + deltaForce);
                body.AddForceAtPosition(bodyUp * deltaForce, leftWheel.ContactPoint);
            }

            if (rightWheel.HasContact)
            {
                rightWheel.NormalLoad = Mathf.Max(0f, rightWheel.NormalLoad - deltaForce);
                body.AddForceAtPosition(-bodyUp * deltaForce, rightWheel.ContactPoint);
            }
        }
    }
}
