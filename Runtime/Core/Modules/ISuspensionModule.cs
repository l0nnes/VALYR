using VALYR.Core;
using VALYR.Core.Interfaces;

namespace VALYR.Core.Modules
{
    /// <summary>
    /// Contract for vehicle suspension dynamics module.
    /// Responsible for computing spring forces, preload equilibrium, asymmetric damping,
    /// progressive bump stop resistance, and applying normal loads Fz to the chassis body.
    /// </summary>
    public interface ISuspensionModule
    {
        /// <summary>
        /// Solves suspension spring, damper, and bump stop dynamics for all 4 wheels.
        /// </summary>
        /// <param name="state">Vehicle state buffer containing wheel contact information.</param>
        /// <param name="body">Physics rigid body abstraction for applying normal force vectors.</param>
        /// <param name="chassis">Chassis mass and wheel mount positions.</param>
        /// <param name="dt">Physics step in seconds.</param>
        void SolveSuspension(
            ref VehicleState state,
            IPhysicsBody body,
            in VehicleChassisConfig chassis,
            float dt);
    }
}
