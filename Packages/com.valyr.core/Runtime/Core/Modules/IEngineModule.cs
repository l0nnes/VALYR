using System;
using UnityEngine;
using VALYR.Core;

namespace VALYR.Core.Modules
{
    /// <summary>
    /// Выходной сигнал силовой установки на текущий физический тик.
    /// Передается трансмиссии.
    /// </summary>
    [Serializable]
    public struct EngineOutput
    {
        [Tooltip("Крутящий момент на коленчатом валу / маховике двигателя (Нм)")]
        public float OutputTorque;

        [Tooltip("Максимальный возможный крутящий момент на текущих RPM (Нм)")]
        public float MaxTorque;

        [Tooltip("Фактический уровень открытия дросселя с учетом регуляторов")]
        public float EffectiveThrottle;
    }

    /// <summary>
    /// Обратная связь от трансмиссии к двигателю на текущий физический шаг.
    /// Передает обороты трансмиссии и нагрузочный момент колес на маховик.
    /// </summary>
    [Serializable]
    public struct TransmissionFeedback
    {
        [Tooltip("Обороты входного вала коробки передач, приведенные к двигателю (RPM)")]
        public float DrivetrainRpm;

        [Tooltip("Момент сопротивления вращению коленвала от трансмиссии и колес (Нм)")]
        public float LoadTorque;

        [Tooltip("Степень замыкания сцепления (0 = разомкнуто, 1 = замкнуто)")]
        public float ClutchEngagement;

        [Tooltip("Флаг нейтральной передачи или процесса выжима сцепления")]
        public bool IsDisengaged;

        public static TransmissionFeedback Free => new()
        {
            DrivetrainRpm = 0f,
            LoadTorque = 0f,
            ClutchEngagement = 0f,
            IsDisengaged = true
        };
    }

    /// <summary>
    /// Контракт модуля двигателя.
    /// Рассчитывает динамику оборотов коленвала (RPM), кривую крутящего момента,
    /// холостой ход (idle governor), отсечку (rev limiter) и торможение двигателем.
    /// </summary>
    public interface IEngineModule
    {
        /// <summary>
        /// Обновляет обороты двигателя и формирует выходной крутящий момент.
        /// </summary>
        /// <param name="input">Команды управления водителя (газ/тормоз)</param>
        /// <param name="feedback">Обратная связь от коробки передач и сцепления</param>
        /// <param name="state">Физическое состояние машины (обновляет state.EngineRpm)</param>
        /// <param name="dt">Шаг времени (секунды)</param>
        /// <param name="output">Выходной крутящий момент на маховике</param>
        void UpdateEngine(
            in VehicleInput input,
            in TransmissionFeedback feedback,
            ref VehicleState state,
            float dt,
            out EngineOutput output);
    }
}
