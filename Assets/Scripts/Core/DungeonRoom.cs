using UnityEngine;

namespace CallansDungeonGenerator
{
    public class DungeonRoom : MonoBehaviour
    {
        [SerializeField]
        private BoxCollider bounds;

        private DungeonDoor[] doors;

        public BoxCollider Bounds => bounds;

        public DungeonDoor[] Doors => doors;

        private void Awake()
        {
            doors = GetComponentsInChildren<DungeonDoor>();
        }
    }
}