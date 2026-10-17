using System;
using UnityEngine;

namespace VALYR.Core
{
    /// <summary>
    /// Буфер команд и моментов на колесо на текущий физический тик.
    /// Заполняется трансмиссией и тормозами, модифицируется электронными помощниками (ABS, TCS, ESP).
    /// </summary>
    [Serializable]
    public struct WheelActuation
    {
        [Tooltip("Крутящий момент привода от двигателя/трансмиссии (Нм)")]
        public float DriveTorque;

        [Tooltip("Тормозной момент от педали тормоза (Нм)")]
        public float BrakeTorque;

        [Tooltip("Тормозной момент от стояночного тормоза (Нм)")]
        public float HandbrakeTorque;

        [Tooltip("Верхний предел момента тяги от TCS (float.MaxValue = без ограничений)")]
        public float MaxDriveTorqueLimit;

        [Tooltip("Верхний предел тормозного момента от ABS (float.MaxValue = без ограничений)")]
        public float MaxBrakeTorqueLimit;

        public void Reset(float maxLimit = float.MaxValue)
        {
            DriveTorque = 0f;
            BrakeTorque = 0f;
            HandbrakeTorque = 0f;
            MaxDriveTorqueLimit = maxLimit;
            MaxBrakeTorqueLimit = maxLimit;
        }

        public float GetEffectiveDriveTorque()
        {
            return Mathf.Min(DriveTorque, MaxDriveTorqueLimit);
        }

        public float GetEffectiveBrakeTorque()
        {
            float requestedBrake = BrakeTorque + HandbrakeTorque;
            return Mathf.Min(requestedBrake, MaxBrakeTorqueLimit);
        }
    }
}
