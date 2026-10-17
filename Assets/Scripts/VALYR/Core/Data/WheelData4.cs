using System;
using System.Runtime.CompilerServices;

namespace VALYR.Core
{
    /// <summary>
    /// Непрерывный буфер данных на 4 колеса (0: Front-Left, 1: Front-Right, 2: Rear-Left, 3: Rear-Right).
    /// Полностью unmanaged/blittable, гарантирует 0 байт аллокаций в Heap и прямой доступ по ref.
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

        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                switch (index)
                {
                    case 0: return ref FL;
                    case 1: return ref FR;
                    case 2: return ref RL;
                    case 3: return ref RR;
                    default: throw new IndexOutOfRangeException($"Wheel index {index} is out of range [0..3].");
                }
            }
        }
    }
}
