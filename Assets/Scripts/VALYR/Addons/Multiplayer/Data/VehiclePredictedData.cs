using System;
using PurrNet.Packing;
using PurrNet.Prediction;
using UnityEngine;
using VALYR.Core;

namespace VALYR.Multiplayer
{
    internal static class BitPackerVehicleExtensions
    {
        public static void WriteFloat(this BitPacker packer, float value)
        {
            int bits = BitConverter.SingleToInt32Bits(value);
            packer.WriteBits((ulong)(uint)bits, 32);
        }

        public static float ReadFloat(this BitPacker packer)
        {
            int bits = (int)(uint)packer.ReadBits(32);
            return BitConverter.Int32BitsToSingle(bits);
        }

        public static void WriteInt(this BitPacker packer, int value)
        {
            packer.WriteBits((ulong)(uint)value, 32);
        }

        public static int ReadInt(this BitPacker packer)
        {
            return (int)(uint)packer.ReadBits(32);
        }

        public static void WriteUInt(this BitPacker packer, uint value)
        {
            packer.WriteBits(value, 32);
        }

        public static uint ReadUInt(this BitPacker packer)
        {
            return (uint)packer.ReadBits(32);
        }
    }

    /// <summary>
    /// Driver input snapshot streamed per tick for client-side prediction.
    /// Implements IPredictedData and IPacked for PurrDiction packing and delta compression.
    /// </summary>
    [Serializable]
    public struct VehiclePredictedInput : IPredictedData, IPacked
    {
        public float Throttle;
        public float Steer;
        public float Brake;
        public float Handbrake;

        public VehiclePredictedInput(float throttle, float steer, float brake, float handbrake)
        {
            Throttle = Mathf.Clamp01(throttle);
            Steer = Mathf.Clamp(steer, -1f, 1f);
            Brake = Mathf.Clamp01(brake);
            Handbrake = Mathf.Clamp01(handbrake);
        }

        public void Write(BitPacker packer)
        {
            packer.WriteFloat(Throttle);
            packer.WriteFloat(Steer);
            packer.WriteFloat(Brake);
            packer.WriteFloat(Handbrake);
        }

        public void Read(BitPacker packer)
        {
            Throttle = packer.ReadFloat();
            Steer = packer.ReadFloat();
            Brake = packer.ReadFloat();
            Handbrake = packer.ReadFloat();
        }

        public void Dispose() { }
    }

    /// <summary>
    /// Vehicle mechanical simulation state snapshot recorded per tick.
    /// Wraps VALYR.Core.VehicleState for PurrDiction rollback replays and verification.
    /// </summary>
    [Serializable]
    public struct VehiclePredictedState : IPredictedData<VehiclePredictedState>, IPacked
    {
        public VehicleState CoreState;

        public void Write(BitPacker packer)
        {
            packer.WriteUInt(CoreState.Tick);
            packer.WriteFloat(CoreState.EngineRpm);
            packer.WriteInt(CoreState.CurrentGear);
            packer.WriteFloat(CoreState.ClutchEngagement);
            packer.WriteFloat(CoreState.ShiftTimer);

            for (int i = 0; i < 4; i++)
            {
                var w = CoreState.Wheels[i];
                packer.WriteFloat(w.AngularVelocity);
                packer.WriteFloat(w.RotationAngle);
                packer.WriteFloat(w.SteerAngle);
                packer.WriteBit(w.HasContact);
                packer.WriteFloat(w.SuspensionLength);
                packer.WriteFloat(w.SuspensionCompression);
                packer.WriteFloat(w.SuspensionVelocity);
                packer.WriteFloat(w.NormalLoad);
                packer.WriteFloat(w.SlipRatio);
                packer.WriteFloat(w.SlipAngle);
                packer.WriteFloat(w.SkidIntensity);
            }
        }

        public void Read(BitPacker packer)
        {
            CoreState.Tick = packer.ReadUInt();
            CoreState.EngineRpm = packer.ReadFloat();
            CoreState.CurrentGear = packer.ReadInt();
            CoreState.ClutchEngagement = packer.ReadFloat();
            CoreState.ShiftTimer = packer.ReadFloat();

            for (int i = 0; i < 4; i++)
            {
                var w = CoreState.Wheels[i];
                w.AngularVelocity = packer.ReadFloat();
                w.RotationAngle = packer.ReadFloat();
                w.SteerAngle = packer.ReadFloat();
                w.HasContact = packer.ReadBit();
                w.SuspensionLength = packer.ReadFloat();
                w.SuspensionCompression = packer.ReadFloat();
                w.SuspensionVelocity = packer.ReadFloat();
                w.NormalLoad = packer.ReadFloat();
                w.SlipRatio = packer.ReadFloat();
                w.SlipAngle = packer.ReadFloat();
                w.SkidIntensity = packer.ReadFloat();
                CoreState.Wheels[i] = w;
            }
        }

        public void Dispose()
        {
            CoreState.Dispose();
        }
    }
}
