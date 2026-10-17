using System;
using UnityEngine;

namespace VALYR.Core
{
    /// <summary>
    /// Полный снимок состояния физики машины для одного тика.
    /// Blittable unmanaged struct: нулевые аллокации в куче при копировании и Rollback-реплеях.
    /// Совместима с интерфейсом PurrDiction IPredictedData.
    /// </summary>
    [Serializable]
    public struct VehicleState
    {
        public uint Tick;

        // --- Состояние силовой установки ---
        [Tooltip("Обороты коленчатого вала двигателя (RPM)")]
        public float EngineRpm;

        [Tooltip("Текущая включенная передача (-1: R, 0: N, 1..N: передачи вперед)")]
        public int CurrentGear;

        [Tooltip("Степень блокировки сцепления (0 = разомкнуто, 1 = жестко замкнуто)")]
        public float ClutchEngagement;

        [Tooltip("Таймер задержки переключения передачи (секунды)")]
        public float ShiftTimer;

        // --- Физическое состояние всех 4 колес ---
        public WheelData4<WheelState> Wheels;

        public void Dispose() { }
    }
}
