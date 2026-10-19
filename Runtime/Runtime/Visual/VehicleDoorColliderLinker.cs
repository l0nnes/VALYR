using System;
using UnityEngine;

namespace VALYR.Runtime.Visual
{
    /// <summary>
    /// Explicitly links and synchronizes physical door/hood/tailgate colliders situated
    /// on the physics Rigidbody tree with animated visual meshes inside the interpolated Graphics container.
    /// Prevents physics jitter while ensuring full physical collision during motion.
    /// </summary>
    [ExecuteAlways]
    public class VehicleDoorColliderLinker : MonoBehaviour
    {
        [Serializable]
        public struct DoorLink
        {
            public string Name;
            [Tooltip("Visual animated mesh transform (inside Graphics).")]
            public Transform VisualTransform;
            [Tooltip("Physical collider transform (under Body Colliders).")]
            public Transform ColliderTransform;
        }

        [Header("Linked Elements")]
        public DoorLink[] Links;

        private void Reset()
        {
            AutoDiscoverLinks();
        }

        [ContextMenu("Auto-Discover Links")]
        public void AutoDiscoverLinks()
        {
            var collidersRoot = transform.Find("Body Colliders");
            var meshesRoot = transform.Find("Graphics/Meshes") ?? transform.Find("Meshes");

            if (collidersRoot == null || meshesRoot == null) return;

            Links = new DoorLink[]
            {
                new DoorLink
                {
                    Name = "Door Left",
                    VisualTransform = meshesRoot.Find("Doors/Door Left") ?? meshesRoot.Find("Door Left"),
                    ColliderTransform = collidersRoot.Find("Door Left Collider") ?? collidersRoot.Find("Door Left")
                },
                new DoorLink
                {
                    Name = "Door Right",
                    VisualTransform = meshesRoot.Find("Doors/Door Right") ?? meshesRoot.Find("Door Right"),
                    ColliderTransform = collidersRoot.Find("Door Right Collider") ?? collidersRoot.Find("Door Right")
                },
                new DoorLink
                {
                    Name = "Hood",
                    VisualTransform = meshesRoot.Find("Body Hood"),
                    ColliderTransform = collidersRoot.Find("Body Hood Collider") ?? collidersRoot.Find("Body Hood")
                },
                new DoorLink
                {
                    Name = "Tailgate",
                    VisualTransform = meshesRoot.Find("Body Trunk Hatch"),
                    ColliderTransform = collidersRoot.Find("Body Trunk Hatch Collider") ?? collidersRoot.Find("Body Trunk Hatch") ?? collidersRoot.Find("Tailgate Collider")
                }
            };
        }

        private void LateUpdate()
        {
            if (Links == null) return;

            for (int i = 0; i < Links.Length; i++)
            {
                var link = Links[i];
                if (link.VisualTransform != null && link.ColliderTransform != null)
                {
                    link.ColliderTransform.localRotation = link.VisualTransform.localRotation;
                }
            }
        }
    }
}
