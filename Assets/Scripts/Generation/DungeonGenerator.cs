using System.Collections.Generic;
using UnityEngine;

namespace CallansDungeonGenerator
{
    public class DungeonGenerator : MonoBehaviour
    {
        [Header("Room Prefabs")]
        [SerializeField] private DungeonRoom startRoom;
        [SerializeField] private DungeonRoom endRoom;
        [SerializeField] private List<DungeonRoom> roomPrefabs;

        [Header("Generation Settings")]
        [SerializeField, Min(2)] private int roomCount = 5;
        [SerializeField, Min(1)] private int maxAttempts = 1000;

        private readonly List<DungeonRoom> placedRooms = new();
        private readonly RoomPlacer roomPlacer = new();

        private int attempts;

        private void Start()
        {
            Generate();
        }

        /// <summary>
        /// Generates a complete main path using recursive backtracking.
        /// </summary>
        public bool Generate()
        {
            // Validate configuration.
            if (startRoom == null ||
                endRoom == null ||
                roomPrefabs == null ||
                roomPrefabs.Count == 0 ||
                roomCount < 2 ||
                maxAttempts < 1)
            {
                Debug.LogError("Invalid dungeon configuration.");
                return false;
            }

            // Reset previous generation state.
            Clear();
            attempts = 0;

            // Instantiate the starting room.
            DungeonRoom firstRoom = Instantiate(
                startRoom,
                transform.position,
                Quaternion.identity,
                transform
            );

            placedRooms.Add(firstRoom);

            // Generate the remaining rooms.
            if (TryGeneratePath(firstRoom))
            {
                Debug.Log(
                    $"Generated {placedRooms.Count} rooms " +
                    $"in {attempts} placement attempts."
                );

                return true;
            }

            Debug.LogWarning(
                $"Dungeon generation failed after {attempts} attempts."
            );

            Clear();
            return false;
        }

        /// <summary>
        /// Recursively extends the main path and backtracks when necessary.
        /// </summary>
        private bool TryGeneratePath(DungeonRoom previousRoom)
        {
            // Stop when we've generated the requested number of rooms.
            if (placedRooms.Count >= roomCount)
                return true;

            if (attempts >= maxAttempts)
                return false;

            // Collect and randomise the available doorways.
            List<DungeonDoor> availableDoors = new();

            foreach (DungeonDoor door in previousRoom.Doors)
            {
                if (door != null && !door.IsConnected)
                    availableDoors.Add(door);
            }

            Shuffle(availableDoors);

            foreach (DungeonDoor targetDoor in availableDoors)
            {
                // Use the ending room when placing the final room.
                bool isLastRoom = placedRooms.Count == roomCount - 1;

                List<DungeonRoom> candidates = isLastRoom
                    ? new List<DungeonRoom> { endRoom }
                    : new List<DungeonRoom>(roomPrefabs);

                Shuffle(candidates);

                foreach (DungeonRoom prefab in candidates)
                {
                    if (attempts >= maxAttempts)
                        return false;

                    if (prefab == null)
                        continue;

                    // Instantiate a candidate and randomise its doorways.
                    DungeonRoom candidate = Instantiate(
                        prefab,
                        Vector3.zero,
                        Quaternion.identity,
                        transform
                    );

                    List<DungeonDoor> candidateDoors = new(candidate.Doors);
                    Shuffle(candidateDoors);

                    foreach (DungeonDoor candidateDoor in candidateDoors)
                    {
                        if (attempts >= maxAttempts)
                            break;

                        if (candidateDoor == null)
                            continue;

                        attempts++;

                        // Attempt to align the candidate without overlaps.
                        if (!roomPlacer.TryPlaceRoom(
                            candidate,
                            candidateDoor,
                            targetDoor,
                            placedRooms))
                        {
                            continue;
                        }

                        // Temporarily accept the placement.
                        candidateDoor.Connect();
                        targetDoor.Connect();
                        placedRooms.Add(candidate);

                        // Attempt to complete the remaining path.
                        if (TryGeneratePath(candidate))
                            return true;

                        // Backtrack if the remaining path cannot be completed.
                        placedRooms.Remove(candidate);
                        candidateDoor.Disconnect();
                        targetDoor.Disconnect();

                        candidate.transform.SetPositionAndRotation(
                            Vector3.zero,
                            Quaternion.identity
                        );
                    }

                    // Discard unsuccessful candidates.
                    Destroy(candidate.gameObject);

                    if (attempts >= maxAttempts)
                        return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Removes all previously generated rooms.
        /// </summary>
        private void Clear()
        {
            foreach (DungeonRoom room in placedRooms)
            {
                if (room != null)
                    Destroy(room.gameObject);
            }

            placedRooms.Clear();
        }

        /// <summary>
        /// Randomises the order of elements in a list.
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