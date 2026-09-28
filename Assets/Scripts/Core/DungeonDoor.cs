using UnityEngine;

namespace CallansDungeonGenerator
{
    public class DungeonDoor : MonoBehaviour
    {
        public DungeonDoor ConnectedDoor { get; private set; }

        public bool IsConnected => ConnectedDoor != null;

        public Vector3 Position => transform.position;

        public Vector3 Forward => transform.forward;

        /// <summary>
        /// Establishes a connection between two doorways.
        /// </summary>
        public bool Connect(DungeonDoor other)
        {
            // Reject invalid or occupied doorways.
            if (other == null ||
                other == this ||
                IsConnected ||
                other.IsConnected)
            {
                return false;
            }

            // Both doorways reference one another.
            ConnectedDoor = other;
            other.ConnectedDoor = this;

            return true;
        }

        /// <summary>
        /// Disconnects both ends of an existing connection.
        /// </summary>
        public void Disconnect()
        {
            if (!IsConnected)
                return;

            DungeonDoor other = ConnectedDoor;

            ConnectedDoor = null;

            if (other != null && other.ConnectedDoor == this)
                other.ConnectedDoor = null;
        }

        /// <summary>
        /// Draws an arrow showing the doorway's forward direction.
        /// </summary>
        private void OnDrawGizmos()
        {
            Gizmos.color = IsConnected ? Color.green : Color.red;

            Vector3 start = transform.position;
            Vector3 end = start + transform.forward * 1.5f;

            Gizmos.DrawLine(start, end);

            Vector3 left = Quaternion.Euler(0, 150, 0)
                * transform.forward;

            Vector3 right = Quaternion.Euler(0, -150, 0)
                * transform.forward;

            Gizmos.DrawLine(end, end + left * 0.4f);
            Gizmos.DrawLine(end, end + right * 0.4f);

            Gizmos.DrawSphere(start, 0.15f);
        }
    }
}