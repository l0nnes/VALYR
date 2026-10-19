using VALYR.Core;
using VALYR.Core.Interfaces;

namespace VALYR.Core.Modules
{
    /// <summary>
    /// Контракт ассистентов шасси и стабилизации (Anti-Roll Bar, аэродинамический прижим / крылья).
    /// Выполняется после расчета подвески для перераспределения нормальных нагрузок (NormalLoad)
    /// или приложения стабилизирующих сил/моментов к кузову.
    /// </summary>
    public interface IChassisAssistModule
    {
        /// <summary>
        /// Применяет стабилизацию шасси.
        /// </summary>
        /// <param name="state">Физическое состояние машины (корректирует NormalLoad колес)</param>
        /// <param name="body">Физическое тело (приложение анти-крен сил/моментов)</param>
        /// <param name="chassis">Геометрия шасси</param>
        /// <param name="dt">Шаг времени (секунды)</param>
        void ApplyChassisAssists(
            ref VehicleState state,
            IPhysicsBody body,
            in VehicleChassisConfig chassis,
            float dt);
    }
}
