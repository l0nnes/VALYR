using UnityEngine;

namespace VALYR.Multiplayer
{
    /// <summary>
    /// Marker component defining vehicle spawn locations in the scene.
    /// </summary>
    [SelectionBase]
    public class MultiplayerSpawnPoint : MonoBehaviour
    {
        public Color GizmoColor = new Color(0.2f, 0.85f, 1f, 0.8f);

        private void OnDrawGizmos()
        {
            Gizmos.color = GizmoColor;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, 1f, 0f), new Vector3(2.2f, 2f, 5f));
            Gizmos.DrawRay(new Vector3(0f, 1f, 0f), Vector3.forward * 3f);
        }
    }
}
