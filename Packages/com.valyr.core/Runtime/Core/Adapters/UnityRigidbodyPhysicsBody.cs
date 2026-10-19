using UnityEngine;
using VALYR.Core.Interfaces;

namespace VALYR.Core.Adapters
{
    /// <summary>
    /// Адаптер стандартного Unity Rigidbody к интерфейсу IPhysicsBody.
    /// Используется для одиночной игры и локальных тестов.
    /// </summary>
    public sealed class UnityRigidbodyPhysicsBody : IPhysicsBody
    {
        private readonly Rigidbody _rigidbody;

        public UnityRigidbodyPhysicsBody(Rigidbody rigidbody)
        {
            _rigidbody = rigidbody;
        }

        public Vector3 Position => _rigidbody.position;
        public Quaternion Rotation => _rigidbody.rotation;
        public Vector3 LinearVelocity => _rigidbody.linearVelocity;
        public Vector3 AngularVelocity => _rigidbody.angularVelocity;

        public Vector3 GetPointVelocity(Vector3 worldPoint)
        {
            return _rigidbody.GetPointVelocity(worldPoint);
        }

        public void AddForceAtPosition(Vector3 force, Vector3 position)
        {
            _rigidbody.AddForceAtPosition(force, position);
        }

        public void AddForce(Vector3 force)
        {
            _rigidbody.AddForce(force);
        }

        public void AddTorque(Vector3 torque)
        {
            _rigidbody.AddTorque(torque);
        }
    }
}
