using VALYR.Core;
using VALYR.Core.Interfaces;

namespace VALYR.Core.Modules
{
    /// <summary>
    /// Contract for vehicle wheel ground collision & contact patch detection subsystem.
    /// Responsible for geometric surface scanning (rays, multi-ray patch, radial sector, spherecast),
    /// identifying road/obstacle contacts, surface normals, and effective damper compression distances.
    /// </summary>
    public interface IWheelContactModule
    {
        /// <summary>
        /// Scans road and obstacle geometry for all 4 vehicle wheels for the current physics tick.
        /// Updates wheel contact states (HasContact, ContactPoint, ContactNormal, SuspensionLength, SurfaceVelocity).
        /// </summary>
        /// <param name="state">Current physical vehicle state buffer.</param>
        /// <param name="body">Chassis rigid body abstraction.</param>
        /// <param name="surfaceProvider">Physics scene surface querying adapter.</param>
        /// <param name="chassis">Vehicle chassis dimensions and wheel mount positions.</param>
        /// <param name="dt">Simulation physics step in seconds.</param>
        void DetectContacts(
            ref VehicleState state,
            IPhysicsBody body,
            ISurfaceProvider surfaceProvider,
            in VehicleChassisConfig chassis,
            float dt);
    }
}
