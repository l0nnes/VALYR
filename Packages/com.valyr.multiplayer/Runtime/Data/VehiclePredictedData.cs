using System;
using PurrNet.Packing;
using PurrNet.Prediction;
using PurrNet.Utils;
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
    /// Explicit packer registration for PurrNet and PurrDiction.
    /// Prepares types in Hasher and registers custom DeltaPacker/Packer handlers
    /// to prevent Hasher reflection exceptions for VALYR.Core types.
    /// </summary>
    public static class VehiclePredictedRegistration
    {
        private static bool _registered;

        static VehiclePredictedRegistration()
        {
            Register();
        }

        [RegisterPackers(priority: 1000)]
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        public static void Register()
        {
            if (_registered)
                return;
            _registered = true;

            // 1. Register types in Hasher to prevent "Type is not registered" exceptions
            Hasher.PrepareType(typeof(VehicleState));
            Hasher.PrepareType(typeof(WheelState));
            Hasher.PrepareType(typeof(WheelData4<WheelState>));
            Hasher.PrepareType(typeof(VehiclePredictedState));
            Hasher.PrepareType(typeof(VehiclePredictedInput));

            // 2. Register DeltaPackers
            DeltaPacker<VehicleState>.Register(WriteVehicleStateDelta, ReadVehicleStateDelta);
            DeltaPacker<VehiclePredictedState>.Register(WritePredictedStateDelta, ReadPredictedStateDelta);
            DeltaPacker<VehiclePredictedInput>.Register(WritePredictedInputDelta, ReadPredictedInputDelta);

            // 3. Register Packers
            Packer<VehicleState>.RegisterWriter(WriteVehicleState);
            Packer<VehicleState>.RegisterReader(ReadVehicleState);

            Packer<VehiclePredictedState>.RegisterWriter(WritePredictedState);
            Packer<VehiclePredictedState>.RegisterReader(ReadPredictedState);

            Packer<VehiclePredictedInput>.RegisterWriter(WritePredictedInput);
            Packer<VehiclePredictedInput>.RegisterReader(ReadPredictedInput);
        }

        public static void WriteVehicleState(BitPacker packer, VehicleState state)
        {
            packer.WriteUInt(state.Tick);
            packer.WriteFloat(state.EngineRpm);
            packer.WriteInt(state.CurrentGear);
            packer.WriteFloat(state.ClutchEngagement);
            packer.WriteFloat(state.ShiftTimer);

            for (int i = 0; i < 4; i++)
            {
                var w = state.Wheels[i];
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

        public static void ReadVehicleState(BitPacker packer, ref VehicleState state)
        {
            state.Tick = packer.ReadUInt();
            state.EngineRpm = packer.ReadFloat();
            state.CurrentGear = packer.ReadInt();
            state.ClutchEngagement = packer.ReadFloat();
            state.ShiftTimer = packer.ReadFloat();

            for (int i = 0; i < 4; i++)
            {
                var w = state.Wheels[i];
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
                state.Wheels[i] = w;
            }
        }

        public static bool WriteVehicleStateDelta(BitPacker packer, VehicleState oldValue, VehicleState newValue)
        {
            WriteVehicleState(packer, newValue);
            return true;
        }

        public static void ReadVehicleStateDelta(BitPacker packer, VehicleState oldValue, ref VehicleState value)
        {
            ReadVehicleState(packer, ref value);
        }

        public static void WritePredictedState(BitPacker packer, VehiclePredictedState state)
        {
            WriteVehicleState(packer, state.CoreState);
            packer.WriteFloat(state.Throttle);
            packer.WriteFloat(state.Steer);
            packer.WriteFloat(state.Brake);
            packer.WriteFloat(state.Handbrake);
        }

        public static void ReadPredictedState(BitPacker packer, ref VehiclePredictedState state)
        {
            ReadVehicleState(packer, ref state.CoreState);
            state.Throttle = packer.ReadFloat();
            state.Steer = packer.ReadFloat();
            state.Brake = packer.ReadFloat();
            state.Handbrake = packer.ReadFloat();
        }

        public static bool WritePredictedStateDelta(BitPacker packer, VehiclePredictedState oldValue, VehiclePredictedState newValue)
        {
            WritePredictedState(packer, newValue);
            return true;
        }

        public static void ReadPredictedStateDelta(BitPacker packer, VehiclePredictedState oldValue, ref VehiclePredictedState value)
        {
            ReadPredictedState(packer, ref value);
        }

        public static void WritePredictedInput(BitPacker packer, VehiclePredictedInput input)
        {
            packer.WriteFloat(input.Throttle);
            packer.WriteFloat(input.Steer);
            packer.WriteFloat(input.Brake);
            packer.WriteFloat(input.Handbrake);
        }

        public static void ReadPredictedInput(BitPacker packer, ref VehiclePredictedInput input)
        {
            input.Throttle = packer.ReadFloat();
            input.Steer = packer.ReadFloat();
            input.Brake = packer.ReadFloat();
            input.Handbrake = packer.ReadFloat();
        }

        public static bool WritePredictedInputDelta(BitPacker packer, VehiclePredictedInput oldValue, VehiclePredictedInput newValue)
        {
            WritePredictedInput(packer, newValue);
            return true;
        }

        public static void ReadPredictedInputDelta(BitPacker packer, VehiclePredictedInput oldValue, ref VehiclePredictedInput value)
        {
            ReadPredictedInput(packer, ref value);
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

        public void Write(BitPacker packer) => VehiclePredictedRegistration.WritePredictedInput(packer, this);
        public void Read(BitPacker packer) => VehiclePredictedRegistration.ReadPredictedInput(packer, ref this);
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
        public float Throttle;
        public float Steer;
        public float Brake;
        public float Handbrake;

        public void Write(BitPacker packer) => VehiclePredictedRegistration.WritePredictedState(packer, this);
        public void Read(BitPacker packer) => VehiclePredictedRegistration.ReadPredictedState(packer, ref this);
        public void Dispose() => CoreState.Dispose();
    }
}
