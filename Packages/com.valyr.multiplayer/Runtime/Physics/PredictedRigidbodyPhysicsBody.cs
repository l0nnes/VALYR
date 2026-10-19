using System;
using UnityEngine;
using PurrNet.Prediction;
using VALYR.Core.Interfaces;

namespace VALYR.Multiplayer
{
    /// <summary>
    /// Bridges VALYR's discrete vehicle physics core to PurrDiction's PredictedRigidbody.
    /// Redirects all force and torque applications through PredictedRigidbody's prediction wrapper,
    /// ensuring forces participate in tick simulation, rollback replays, and PhysX sub-stepping.
    /// </summary>
    public sealed class PredictedRigidbodyPhysicsBody : IPhysicsBody
    {
        private readonly PredictedRigidbody _body;
        private readonly Rigidbody _rb;

        public PredictedRigidbodyPhysicsBody(PredictedRigidbody body)
        {
            _body = body ?? throw new ArgumentNullException(nameof(body));
            _rb = body.rigidbody != null ? body.rigidbody : body.GetComponent<Rigidbody>();
        }

        public Vector3 Position => _body.position;
        public Quaternion Rotation => _body.rotation;
        public Vector3 LinearVelocity => _body.linearVelocity;
        public Vector3 AngularVelocity => _body.angularVelocity;

        public Vector3 GetPointVelocity(Vector3 worldPoint)
        {
            return _rb != null ? _rb.GetPointVelocity(worldPoint) : Vector3.zero;
        }

        public void AddForceAtPosition(Vector3 force, Vector3 position)
        {
            _body.AddForceAtPosition(force, position, ForceMode.Force);
        }

        public void AddForce(Vector3 force)
        {
            _body.AddForce(force, ForceMode.Force);
        }

        public void AddTorque(Vector3 torque)
        {
            _body.AddTorque(torque, ForceMode.Force);
        }
    }
}
