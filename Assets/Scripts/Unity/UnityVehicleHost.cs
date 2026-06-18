using System.Collections.Generic;
using Core;
using Core.Interfaces;
using Core.Math;
using UnityEngine;

namespace Unity
{
    public class UnityVehicleHost : IVehicleHost
    {
        private readonly Rigidbody _rigidbody;

        private readonly Dictionary<int, WheelSweeper> _sweepers = new();
        private readonly Transform _transform;
        private bool _cachedDownshift;
        private bool _cachedUpshift;
        private InputState _currentInput;

        public UnityVehicleHost(Rigidbody rb)
        {
            _rigidbody = rb;
            _transform = rb.transform;
        }

        public float DeltaTime => Time.fixedDeltaTime;

        public InputState GetInput()
        {
            (_cachedUpshift, _cachedDownshift) = (false, false);
            return _currentInput;
        }

        public Vec3 GetPosition()
        {
            return _transform.position;
        }

        public Quat GetRotation()
        {
            return _transform.rotation;
        }

        public Vec3 GetLinearVelocity()
        {
            return _rigidbody.linearVelocity;
        }

        public Vec3 GetAngularVelocity()
        {
            return _rigidbody.angularVelocity;
        }

        public Vec3 GetPointVelocity(Vec3 worldPoint)
        {
            return _rigidbody.GetPointVelocity(worldPoint);
        }

        public Vec3 GetLocalPointVelocity(Vec3 worldPoint)
        {
            var worldVelocity = _rigidbody.GetPointVelocity(worldPoint);
            return _transform.InverseTransformVector(worldVelocity);
        }

        public bool Raycast(Vec3 origin, Vec3 direction, float maxDistance, out RaycastResult result)
        {
            if (Physics.Raycast(origin, direction, out var hit, maxDistance))
            {
                result = new RaycastResult
                {
                    Point = hit.point,
                    Normal = hit.normal,
                    Distance = hit.distance,
                    SurfaceVelocity = GetSurfaceVelocity(hit.collider, hit.point)
                };
                return true;
            }

            result = default;
            return false;
        }

        public bool SweepWheel(int wheelIndex, Vec3 origin, Quat rotation, Vec3 direction, float distance, float radius,
            float width, out RaycastResult result)
        {
            if (_sweepers.TryGetValue(wheelIndex, out var sweeper))
                return sweeper.Sweep(origin, rotation, direction, distance, radius, width, out result);

            sweeper = new WheelSweeper(wheelIndex, _rigidbody);
            _sweepers.Add(wheelIndex, sweeper);

            return sweeper.Sweep(origin, rotation, direction, distance, radius, width, out result);
        }

        public void ApplyForce(Vec3 force, Vec3 position)
        {
            _rigidbody.AddForceAtPosition(force, position);
        }

        public void SetInput(float throttle, float steering, float brake, bool handbrake, bool isUpshift,
            bool isDownshift, float clutch)
        {
            _currentInput.throttle = throttle;
            _currentInput.steering = steering;
            _currentInput.handbrake = handbrake;
            _currentInput.brake = brake;
            _currentInput.clutch = clutch;

            _cachedDownshift |= isDownshift;
            _cachedUpshift |= isUpshift;

            _currentInput.isUpshift = _cachedUpshift;
            _currentInput.isDownshift = _cachedDownshift;
        }

        private static Vec3 GetSurfaceVelocity(Collider col, Vector3 point)
        {
            if (!col || !col.attachedRigidbody) return Vec3.Zero;
            return col.attachedRigidbody.GetPointVelocity(point);
        }

        private class WheelSweeper
        {
            private readonly MeshCollider _collider;
            private readonly Mesh _mesh;
            private readonly Rigidbody _rb;

            private float _cachedRadius = -1f;
            private float _cachedWidth = -1f;

            public WheelSweeper(int index, Rigidbody carBody)
            {
                var go = new GameObject($"_WheelSweeper_{index}_");
                go.transform.SetParent(carBody.transform);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;

                go.hideFlags = HideFlags.HideAndDontSave;

                _rb = go.AddComponent<Rigidbody>();
                _rb.isKinematic = true;
                _rb.useGravity = false;
                _rb.interpolation = RigidbodyInterpolation.None;

                _collider = go.AddComponent<MeshCollider>();
                _collider.convex = true;

                _mesh = new Mesh { name = $"SweeperMesh_{index}" };

                var carColliders = carBody.GetComponentsInChildren<Collider>();
                foreach (var col in carColliders)
                {
                    if (!col) continue;
                    Physics.IgnoreCollision(_collider, col, true);
                }
            }

            public bool Sweep(Vector3 origin, Quaternion rotation, Vector3 direction, float distance, float radius,
                float width, out RaycastResult result)
            {
                if (Mathf.Abs(_cachedRadius - radius) > 0.001f || Mathf.Abs(_cachedWidth - width) > 0.001f)
                    GenerateMesh(radius, width);

                _rb.transform.position = origin;
                _rb.transform.rotation = rotation;

                if (_rb.SweepTest(direction, out var hit, distance, QueryTriggerInteraction.Ignore))
                {
                    result = new RaycastResult
                    {
                        Point = hit.point,
                        Normal = hit.normal,
                        Distance = hit.distance + radius,
                        SurfaceVelocity = GetSurfaceVelocity(hit.collider, hit.point)
                    };
                    return true;
                }

                result = default;
                return false;
            }

            private void GenerateMesh(float radius, float width)
            {
                _cachedRadius = radius;
                _cachedWidth = width;

                var segments = 24;
                var vertices = new List<Vector3>();
                var triangles = new List<int>();

                var halfWidth = width * 0.5f;

                vertices.Add(new Vector3(-halfWidth, 0, 0));
                vertices.Add(new Vector3(halfWidth, 0, 0));

                for (var i = 0; i < segments; i++)
                {
                    var angle = (float)i / segments * Mathf.PI * 2;
                    var y = Mathf.Sin(angle) * radius;
                    var z = Mathf.Cos(angle) * radius;

                    vertices.Add(new Vector3(-halfWidth, y, z));
                    vertices.Add(new Vector3(halfWidth, y, z));
                }

                var ringStart = 2;
                for (var i = 0; i < segments; i++)
                {
                    var current = i * 2;
                    var next = (i + 1) % segments * 2;

                    var L1 = ringStart + current;
                    var R1 = ringStart + current + 1;
                    var L2 = ringStart + next;
                    var R2 = ringStart + next + 1;

                    triangles.Add(L1);
                    triangles.Add(R1);
                    triangles.Add(L2);
                    triangles.Add(R1);
                    triangles.Add(R2);
                    triangles.Add(L2);

                    triangles.Add(0);
                    triangles.Add(L2);
                    triangles.Add(L1);

                    triangles.Add(1);
                    triangles.Add(R1);
                    triangles.Add(R2);
                }

                _mesh.Clear();
                _mesh.SetVertices(vertices);
                _mesh.SetTriangles(triangles, 0);
                _mesh.RecalculateNormals();

                _collider.sharedMesh = _mesh;
            }
        }
    }
}