using System.Collections.Generic;
using UnityEngine;

namespace CallansDungeonGenerator
{
    public class DungeonGenerator : MonoBehaviour
    {
        [Header("Room Prefabs")]
        [SerializeField] private DungeonRoom startRoom;
        [SerializeField] private List<DungeonRoom> roomPrefabs;

        [Header("Generation Settings")]
        [SerializeField] private int roomCount = 5;

        private readonly List<DungeonRoom> placedRooms = new();
        private readonly RoomPlacer roomPlacer = new();

        private void Start()
        {
            Generate();
        }

        /// <summary>
        /// Generates a dungeon by extending a path from the starting room.
        /// </summary>
        public void Generate()
        {
            // Validate our configuration.
            if (startRoom == null || roomPrefabs.Count == 0 || roomCount < 1)
            {
                Debug.LogError("Invalid dungeon configuration.");
                return;
            }

            // Instantiate the starting room.
            DungeonRoom currentRoom = Instantiate(
                startRoom,
                transform.position,
                Quaternion.identity,
                transform
            );

            placedRooms.Add(currentRoom);

            // Extend the path until we've reached our target room count.
            while (placedRooms.Count < roomCount)
            {
                if (!TryPlaceNextRoom(currentRoom, out DungeonRoom nextRoom))
                {
                    Debug.LogWarning("Could not place another room.");
                    break;
                }

                placedRooms.Add(nextRoom);
                currentRoom = nextRoom;
            }
        }

        /// <summary>
        /// Attempts to connect a randomly selected room to the previous room.
        /// </summary>
        private bool TryPlaceNextRoom(
            DungeonRoom previousRoom,
            out DungeonRoom nextRoom)
        {
            nextRoom = null;

            // Collect the previous room's available doorways.
            List<DungeonDoor> availableDoors = new();

            foreach (DungeonDoor door in previousRoom.Doors)
            {
                if (!door.IsConnected)
                    availableDoors.Add(door);
            }

            // Try available doorways in a random order.
            Shuffle(availableDoors);

            foreach (DungeonDoor targetDoor in availableDoors)
            {
                // Try our room prefabs in a random order.
                List<DungeonRoom> candidates = new(roomPrefabs);
                Shuffle(candidates);

                foreach (DungeonRoom prefab in candidates)
                {
                    // Create a candidate room.
                    DungeonRoom candidate = Instantiate(
                        prefab,
                        Vector3.zero,
                        Quaternion.identity,
                        transform
                    );

                    // Try each of the candidate's doorways.
                    List<DungeonDoor> candidateDoors = new(candidate.Doors);
                    Shuffle(candidateDoors);

                    foreach (DungeonDoor candidateDoor in candidateDoors)
                    {
                        if (!roomPlacer.TryPlaceRoom(
                            candidate,
                            candidateDoor,
                            targetDoor,
                            placedRooms))
                        {
                            continue;
                        }

                        // Accept the placement and connect both doorways.
                        candidateDoor.Connect();
                        targetDoor.Connect();

                        nextRoom = candidate;
                        return true;
                    }

                    // None of this candidate's doorways produced a valid placement.
                    Destroy(candidate.gameObject);
                }
            }

            return false;
        }

        /// <summary>
        /// Randomizes the order of elements in a list.
        /// </summary>
        private void Shuffle<T>(List<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);

                (items[i], items[randomIndex]) =
                    (items[randomIndex], items[i]);
            }
        }
    }
}