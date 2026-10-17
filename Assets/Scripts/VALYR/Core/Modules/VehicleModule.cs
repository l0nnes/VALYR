using System;
using UnityEngine;

namespace VALYR.Core.Modules
{
    /// <summary>
    /// Конфигурация геометрии и расположения колес шасси относительно центра автомобиля.
    /// </summary>
    [Serializable]
    public struct VehicleChassisConfig
    {
        [Tooltip("Локальные точки крепления подвески 4 колес (FL, FR, RL, RR) относительно кузова")]
        public WheelData4<Vector3> WheelOffsets;

        [Tooltip("Радиусы 4 колес в метрах")]
        public WheelData4<float> WheelRadii;

        [Tooltip("Масса кузова (кг)")]
        public float ChassisMass;

        public static VehicleChassisConfig Default => new()
        {
            WheelOffsets = new WheelData4<Vector3>(
                new Vector3(-0.85f, 0f, 1.35f),  // FL
                new Vector3(0.85f, 0f, 1.35f),   // FR
                new Vector3(-0.85f, 0f, -1.35f), // RL
                new Vector3(0.85f, 0f, -1.35f)   // RR
            ),
            WheelRadii = new WheelData4<float>(0.34f, 0.34f, 0.34f, 0.34f),
            ChassisMass = 1450f
        };
    }

    /// <summary>
    /// Базовый класс для всех сменных функциональных модулей автомобиля.
    /// Модули сериализуются через [SerializeReference] в инспекторе.
    /// </summary>
    [Serializable]
    public abstract class VehicleModule
    {
        [Tooltip("Уникальный идентификатор модуля")]
        public string ModuleId;

        [Tooltip("Включен ли модуль в симуляции")]
        public bool IsEnabled = true;

        /// <summary>
        /// Инициализация модуля геометрией шасси и кэширование параметров.
        /// </summary>
        public virtual void Initialize(in VehicleChassisConfig chassis) { }

        /// <summary>
        /// Сброс внутренних временных параметров (при телепорте, смене сцены).
        /// </summary>
        public virtual void Reset() { }
    }
}
