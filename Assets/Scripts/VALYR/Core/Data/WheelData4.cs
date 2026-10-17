using System;
using System.Runtime.CompilerServices;

namespace VALYR.Core
{
    /// <summary>
    /// Непрерывный буфер данных на 4 колеса (0: Front-Left, 1: Front-Right, 2: Rear-Left, 3: Rear-Right).
    /// Полностью unmanaged/blittable, гарантирует 0 байт аллокаций в Heap.
    /// </summary>
    [Serializable]
    public struct WheelData4<T> where T : struct
    {
        public const int Count = 4;

        public T FL; // Index 0: Front Left
        public T FR; // Index 1: Front Right
        public T RL; // Index 2: Rear Left
        public T RR; // Index 3: Rear Right

        public WheelData4(T fl, T fr, T rl, T rr)
        {
            FL = fl;
            FR = fr;
            RL = rl;
            RR = rr;
        }

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                switch (index)
                {
                    case 0: return FL;
                    case 1: return FR;
                    case 2: return RL;
                    case 3: return RR;
                    default: throw new IndexOutOfRangeException($"Wheel index {index} is out of range [0..3].");
                }
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                switch (index)
                {
                    case 0: FL = value; break;
                    case 1: FR = value; break;
                    case 2: RL = value; break;
                    case 3: RR = value; break;
                    default: throw new IndexOutOfRangeException($"Wheel index {index} is out of range [0..3].");
                }
            }
        }
    }

    /// <summary>
    /// Методы расширения для доступа по ссылке (ref) к элементам WheelData4 без копирования структур.
    /// </summary>
    public static class WheelData4Extensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T GetRef<T>(ref this WheelData4<T> data, int index) where T : struct
        {
            switch (index)
            {
                case 0: return ref data.FL;
                case 1: return ref data.FR;
                case 2: return ref data.RL;
                case 3: return ref data.RR;
                default: throw new IndexOutOfRangeException($"Wheel index {index} is out of range [0..3].");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Reset(ref this WheelData4<WheelActuation> data, float maxLimit = float.MaxValue)
        {
            data.FL.Reset(maxLimit);
            data.FR.Reset(maxLimit);
            data.RL.Reset(maxLimit);
            data.RR.Reset(maxLimit);
        }
    }
}
