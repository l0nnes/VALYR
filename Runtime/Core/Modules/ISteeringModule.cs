using VALYR.Core;
using VALYR.Core.Interfaces;

namespace VALYR.Core.Modules
{
    /// <summary>
    /// Контракт модуля рулевого управления.
    /// Отвечает за вычисление углов поворота колес (SteerAngle) с учетом ввода водителя,
    /// скорости автомобиля (speed-sensitive steering), геометрии Аккермана или параллельного руления.
    /// </summary>
    public interface ISteeringModule
    {
        /// <summary>
        /// Рассчитывает углы поворота колес за текущий физический шаг.
        /// </summary>
        /// <param name="input">Команды ввода водителя (Steer от -1 до +1)</param>
        /// <param name="state">Физическое состояние машины (записывает SteerAngle в колеса)</param>
        /// <param name="body">Физическое тело (для считывания продольной скорости шасси)</param>
        /// <param name="chassis">Конфигурация шасси</param>
        /// <param name="dt">Шаг времени (секунды)</param>
        void UpdateSteering(
            in VehicleInput input,
            ref VehicleState state,
            IPhysicsBody body,
            in VehicleChassisConfig chassis,
            float dt);
    }
}
