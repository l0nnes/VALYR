using System;
using VALYR.Core;

namespace VALYR.Core.Modules
{
    /// <summary>
    /// Контракт модуля трансмиссии автомобиля.
    /// Отвечает за коробку передач (автомат/механика), сцепление, гидротрансформатор,
    /// передаточные числа, дифференциалы (AWD / RWD / FWD, открытый, заблокированный, LSD)
    /// и распределение крутящего момента по ведущим колесам.
    /// Также распределяет тормозные усилия от педали тормоза и стояночного тормоза.
    /// </summary>
    public interface ITransmissionModule
    {
        /// <summary>
        /// Выполняет расчет трансмиссии за физический шаг.
        /// </summary>
        /// <param name="input">Ввод водителя (газ, тормоз, ручник)</param>
        /// <param name="engineOutput">Крутящий момент, пришедший от двигателя</param>
        /// <param name="state">Текущее состояние машины (читает угловые скорости колес, мутирует передачу и сцепление)</param>
        /// <param name="actuation">Буфер моментов на колеса (заполняет DriveTorque, BrakeTorque, HandbrakeTorque)</param>
        /// <param name="chassis">Конфигурация шасси и колес</param>
        /// <param name="dt">Физический шаг времени (секунды)</param>
        /// <param name="feedback">Обратная связь по оборотам и моменту нагрузки для двигателя</param>
        void UpdateTransmission(
            in VehicleInput input,
            in EngineOutput engineOutput,
            ref VehicleState state,
            ref WheelData4<WheelActuation> actuation,
            in VehicleChassisConfig chassis,
            float dt,
            out TransmissionFeedback feedback);
    }
}
