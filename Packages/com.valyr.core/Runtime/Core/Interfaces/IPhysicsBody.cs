using UnityEngine;

namespace VALYR.Core.Interfaces
{
    /// <summary>
    /// Абстракция физического кузова автомобиля.
    /// Позволяет ядру симуляции работать как со стандартным Unity Rigidbody,
    /// так и с PurrDiction PredictedRigidbody без изменения физического кода.
    /// </summary>
    public interface IPhysicsBody
    {
        Vector3 Position { get; }
        Quaternion Rotation { get; }
        Vector3 LinearVelocity { get; }
        Vector3 AngularVelocity { get; }

        /// <summary>
        /// Возвращает линейную скорость произвольной точки кузова в мировых координатах.
        /// </summary>
        Vector3 GetPointVelocity(Vector3 worldPoint);

        /// <summary>
        /// Прикладывает силу в заданной мировой точке кузова.
        /// </summary>
        void AddForceAtPosition(Vector3 force, Vector3 position);

        /// <summary>
        /// Прикладывает силу к центру масс кузова.
        /// </summary>
        void AddForce(Vector3 force);

        /// <summary>
        /// Прикладывает крутящий момент к кузову.
        /// </summary>
        void AddTorque(Vector3 torque);
    }
}
