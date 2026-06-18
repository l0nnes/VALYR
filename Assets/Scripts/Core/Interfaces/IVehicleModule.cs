using Core.Configs;
using Core.Contexts;

namespace Core.Interfaces
{
    public interface IVehicleModule
    {
        VehicleModulePhase Phase { get; }
        void SetConfiguration(ModuleConfig config);
        void Initialize(VehicleContext context);
        void Update(VehicleContext context, float dt);
    }
}