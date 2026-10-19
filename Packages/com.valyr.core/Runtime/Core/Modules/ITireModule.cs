using VALYR.Core;
using VALYR.Core.Interfaces;

namespace VALYR.Core.Modules
{
    /// <summary>
    /// Контракт модуля модели шин (Brush Model, Pacejka, Fiala и т.д.).
    /// Отвечает за вычисление скольжений (SlipRatio, SlipAngle), продольных и поперечных сил сцепления,
    /// интеграцию вращательной динамики колес (WheelDynamics) и приложение результирующих сил к кузову.
    /// </summary>
    public interface ITireModule
    {
        /// <summary>
        /// Выполняет расчет сцепления и динамики всех 4 колес за физический шаг.
        /// </summary>
        /// <param name="state">Физическое состояние машины (обновляет AngularVelocity, RotationAngle, SlipRatio, SlipAngle, TireForce, SkidIntensity)</param>
        /// <param name="actuation">Буфер моментов привода и тормозов на колесах</param>
        /// <param name="body">Физическое тело (для считывания скоростей пятна контакта и приложения сил сцепления)</param>
        /// <param name="chassis">Конфигурация шасси и колес</param>
        /// <param name="dt">Шаг времени (секунды)</param>
        void SolveTires(
            ref VehicleState state,
            in WheelData4<WheelActuation> actuation,
            IPhysicsBody body,
            in VehicleChassisConfig chassis,
            float dt);
    }
}
