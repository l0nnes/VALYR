using UnityEngine;
using PurrNet;

namespace VALYR.Multiplayer
{
    /// <summary>
    /// Custom NetworkRigidbodySettings implementing the Arctic Drive de-penetration and kinematic proxy pattern.
    /// Prevents vehicles from clipping into terrain, rocks, or falling through the world during hard teleports,
    /// while smoothly driving kinematic proxy vehicles with MovePosition/MoveRotation for stable multiplayer collisions.
    /// </summary>
    [CreateAssetMenu(fileName = "VehicleRigidbodySettings", menuName = "VALYR/Multiplayer/Vehicle Rigidbody Settings", order = 100)]
    public class VehicleRigidbodySettings : NetworkRigidbodySettings
    {
        [Header("Depenetration Safe Teleport (Arctic Drive Pattern)")]
        [Tooltip("Max penetration correction iterations to push vehicle out of overlapping colliders.")]
        public int MaxDepenetrationPasses = 4;

        [Tooltip("Extra clearance distance (skin width) added when resolving penetration.")]
        public float SkinWidth = 0.05f;

        [Tooltip("Solid obstacle layer mask used for collision check during teleports.")]
        public LayerMask SolidLayers = ~((1 << 2) | (1 << 3)); // Exclude IgnoreRaycast (2) and Car (3)

        public override NetworkRigidbodySettingsInstance Create(NetworkRigidbody networkRigidbody)
        {
            return new VehicleRigidbodySettingsInstance(this, networkRigidbody);
        }
    }

    /// <summary>
    /// Runtime instance that executes dynamic spring-damper corrections, kinematic proxy movement, and de-penetration logic.
    /// </summary>
    public class VehicleRigidbodySettingsInstance : NetworkRigidbodySettingsInstance
    {
        private readonly VehicleRigidbodySettings _settings;
        private readonly NetworkRigidbody _networkRigidbody;
        private readonly Collider[] _overlapBuffer = new Collider[16];
        private BoxCollider _boxCollider;

        public VehicleRigidbodySettingsInstance(VehicleRigidbodySettings settings, NetworkRigidbody networkRigidbody)
        {
            _settings = settings;
            _networkRigidbody = networkRigidbody;
            if (networkRigidbody != null)
            {
                _boxCollider = networkRigidbody.GetComponentInChildren<BoxCollider>();
            }
        }

        public override void ApplyHardCorrection(in RigidbodyCorrectionContext ctx)
        {
            var rb = ctx.rigidbody;
            if (rb == null) return;

            Vector3 targetPos = ctx.targetPosition;
            Quaternion targetRot = NormalizeQuaternion(ctx.targetRotation);

            // Lazy fetch BoxCollider if not yet cached
            if (_boxCollider == null)
            {
                _boxCollider = rb.GetComponentInChildren<BoxCollider>();
            }

            // Arctic Drive Safe Depenetration: push target position away from ground / obstacle penetrations
            if (_boxCollider != null && _settings != null)
            {
                Vector3 center = targetPos + targetRot * _boxCollider.center;
                Vector3 halfExtents = Vector3.Scale(_boxCollider.size * 0.5f, rb.transform.lossyScale);

                int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, _overlapBuffer, targetRot, _settings.SolidLayers, QueryTriggerInteraction.Ignore);
                if (hitCount > 0)
                {
                    for (int pass = 0; pass < _settings.MaxDepenetrationPasses; pass++)
                    {
                        bool resolvedAny = false;
                        for (int i = 0; i < hitCount; i++)
                        {
                            var other = _overlapBuffer[i];
                            if (other == null || other == _boxCollider || other.transform.IsChildOf(rb.transform))
                                continue;

                            if (Physics.ComputePenetration(
                                _boxCollider, center, targetRot,
                                other, other.transform.position, other.transform.rotation,
                                out Vector3 dir, out float dist))
                            {
                                center += dir * (dist + _settings.SkinWidth);
                                resolvedAny = true;
                            }
                        }

                        if (!resolvedAny)
                            break;
                    }

                    targetPos = center - targetRot * _boxCollider.center;
                }
            }

            rb.position = targetPos;
            rb.rotation = targetRot;
            SetLinearVelocity(rb, ctx.targetLinearVelocity);
            SetAngularVelocity(rb, ctx.targetAngularVelocity);
        }

        public override void ApplyPositionCorrection(in RigidbodyCorrectionContext ctx)
        {
            var rb = ctx.rigidbody;
            if (rb == null) return;

            // When proxy vehicle is Kinematic, drive it directly to the predicted target position with MovePosition.
            // ctx.targetPosition is already Hermite-interpolated and predicted by PurrNet NetworkRigidbody.
            // Avoiding secondary lerping eliminates steady-state lag and keeps proxy at real predicted position.
            if (rb.isKinematic)
            {
                rb.MovePosition(ctx.targetPosition);
                return;
            }

            base.ApplyPositionCorrection(ctx);
        }

        public override void ApplyRotationCorrection(in RigidbodyCorrectionContext ctx)
        {
            var rb = ctx.rigidbody;
            if (rb == null) return;

            // When proxy vehicle is Kinematic, drive it directly to the predicted target rotation with MoveRotation.
            if (rb.isKinematic)
            {
                rb.MoveRotation(NormalizeQuaternion(ctx.targetRotation));
                return;
            }

            base.ApplyRotationCorrection(ctx);
        }
    }
}
