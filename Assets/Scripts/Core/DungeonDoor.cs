using UnityEngine;

namespace CallansDungeonGenerator
{
    public class DungeonDoor : MonoBehaviour
    {
        public bool IsConnected { get; private set; }

        public Vector3 Position => transform.position;

        public Vector3 Forward => transform.forward;

        public void Connect()
        {
            IsConnected = true;
        }

        public void Disconnect()
        {
            IsConnected = false;
        }

        /// <summary>
        /// Draws an arrow showing the doorway's forward direction.
        /// </summary>
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;

            // Draw a line extending outward from the doorway.
            Vector3 start = transform.position;
            Vector3 end = start + transform.forward * 1.5f;

            Gizmos.DrawLine(start, end);

            // Draw the arrowhead.
            Vector3 left = Quaternion.Euler(0, 150, 0) * transform.forward;
            Vector3 right = Quaternion.Euler(0, -150, 0) * transform.forward;

            Gizmos.DrawLine(end, end + left * 0.4f);
            Gizmos.DrawLine(end, end + right * 0.4f);

            // Draw a sphere at the doorway's position.
            Gizmos.DrawSphere(start, 0.15f);
        }
    }
}