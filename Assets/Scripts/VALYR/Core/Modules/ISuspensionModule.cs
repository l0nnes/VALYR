using VALYR.Core;
using VALYR.Core.Interfaces;

namespace VALYR.Core.Modules
{
    /// <summary>
    /// Контракт модуля подвески автомобиля.
    /// Отвечает за сканирование поверхности (лучи/сферы), расчет сил упругости пружин,
    /// демпфирования, нелинейных отбойников (bump stops) и передачу нормальной нагрузки Fz на кузов.
    /// </summary>
    public interface ISuspensionModule
    {
        /// <summary>
        /// Выполняет расчет подвески для всех 4 колес за текущий физический тик.
        /// </summary>
        /// <param name="state">Физическое состояние машины (обновляет NormalLoad, SuspensionCompression, SuspensionVelocity, ContactPoint, ContactNormal)</param>
        /// <param name="body">Абстракция физического тела (приложение сил реакции к кузову)</param>
        /// <param name="surfaceProvider">Провайдер лучевого сканирования поверхности</param>
        /// <param name="chassis">Конфигурация геометрии и расположения колес</param>
        /// <param name="dt">Шаг времени физического тика (секунды)</param>
        void SolveSuspension(
            ref VehicleState state,
            IPhysicsBody body,
            ISurfaceProvider surfaceProvider,
            in VehicleChassisConfig chassis,
            float dt);
    }
}
