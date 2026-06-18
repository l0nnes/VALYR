using Core.Math;

namespace Core.Interfaces
{
    public interface IVehicleHost
    {
        float DeltaTime { get; }
        InputState GetInput();
        Vec3 GetPosition();
        Quat GetRotation();
        Vec3 GetLinearVelocity();
        Vec3 GetAngularVelocity();
        Vec3 GetPointVelocity(Vec3 worldPoint);
        Vec3 GetLocalPointVelocity(Vec3 worldPoint);

        bool Raycast(Vec3 origin, Vec3 direction, float maxDistance, out RaycastResult result);

        bool SweepWheel(int wheelIndex, Vec3 origin, Quat rotation, Vec3 direction, float distance, float radius,
            float width, out RaycastResult result);

        void ApplyForce(Vec3 force, Vec3 position);
    }

    public struct RaycastResult
    {
        public Vec3 Point;
        public Vec3 Normal;
        public float Distance;
        public Vec3 SurfaceVelocity;
    }
}