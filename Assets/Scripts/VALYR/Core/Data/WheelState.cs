using System;
using UnityEngine;

namespace VALYR.Core
{
    /// <summary>
    /// Полное физическое состояние одного колеса на начало/конец тика симуляции.
    /// Хранит кинематику, сжатие подвески, контакт и силы пятна сцепления.
    /// </summary>
    [Serializable]
    public struct WheelState
    {
        // --- Кинематика вращения колеса ---
        [Tooltip("Угловая скорость вращения колеса (рад/с)")]
        public float AngularVelocity;

        [Tooltip("Накопленный визуальный угол поворота колеса (рад)")]
        public float RotationAngle;

        [Tooltip("Текущий угол поворота колеса вокруг вертикальной оси (рад)")]
        public float SteerAngle;

        // --- Подвеска и контакт с поверхностью ---
        [Tooltip("Есть ли касание колеса с поверхностью в данном тике")]
        public bool HasContact;

        [Tooltip("Точка контакта в мировых координатах")]
        public Vector3 ContactPoint;

        [Tooltip("Нормаль поверхности в точке контакта")]
        public Vector3 ContactNormal;

        [Tooltip("Сжатие подвески в метрах (0 = полностью разжата)")]
        public float SuspensionCompression;

        [Tooltip("Скорость сжатия подвески в м/с (+ сжатие, - отбой)")]
        public float SuspensionVelocity;

        [Tooltip("Результирующая нормальная нагрузка на колесо (Ньютоны, Fz)")]
        public float NormalLoad;

        // --- Силы шины и сцепление ---
        [Tooltip("Продольный коэффициент проскальзывания (Longitudinal Slip, sx)")]
        public float SlipRatio;

        [Tooltip("Боковой угол/коэффициент увода (Lateral Slip, sy)")]
        public float SlipAngle;

        [Tooltip("Результирующая сила сцепления шины в мировых координатах (Ньютоны)")]
        public Vector3 TireForce;

        [Tooltip("Интенсивность скольжения 0..1 (для эффектов звука визга и следов шин в UpdateView)")]
        public float SkidIntensity;
    }
}
