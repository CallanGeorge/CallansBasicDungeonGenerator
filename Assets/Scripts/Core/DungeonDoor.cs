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
    }
}