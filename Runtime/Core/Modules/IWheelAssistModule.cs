using VALYR.Core;
using VALYR.Core.Interfaces;

namespace VALYR.Core.Modules
{
    /// <summary>
    /// Контракт модуля электронных помощников колес (ABS, TCS, ESP).
    /// Модули помощников считывают срыв сцепления, угловые скорости колес и занос кузова,
    /// и ограничивают/модулируют моменты в WheelActuation (MaxBrakeTorqueLimit, MaxDriveTorqueLimit).
    /// </summary>
    public interface IWheelAssistModule
    {
        /// <summary>
        /// Применяет ассистенты к буферу моментов колес.
        /// </summary>
        /// <param name="input">Команды ввода водителя</param>
        /// <param name="state">Физическое состояние машины (угловые скорости, slip ratio, skid intensity)</param>
        /// <param name="actuation">Буфер моментов (модифицирует пределы тяги и торможения)</param>
        /// <param name="body">Физическое тело (для считывания продольной/угловой скорости)</param>
        /// <param name="chassis">Геометрия шасси</param>
        /// <param name="dt">Шаг времени (секунды)</param>
        void ApplyAssists(
            in VehicleInput input,
            in VehicleState state,
            ref WheelData4<WheelActuation> actuation,
            IPhysicsBody body,
            in VehicleChassisConfig chassis,
            float dt);
    }
}
