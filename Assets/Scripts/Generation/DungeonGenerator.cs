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

        [Header("Branch Settings")]
        [SerializeField, Range(0f, 1f)] private float branchChance = 0.5f;
        [SerializeField, Min(0)] private int maxBranchRooms = 10;
        [SerializeField, Min(1)] private int minBranchLength = 2;
        [SerializeField, Min(1)] private int maxBranchLength = 4;

        private readonly List<DungeonRoom> placedRooms = new();
        private readonly RoomPlacer roomPlacer = new();

        private int attempts;
        private int branchRoomsPlaced;

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
            branchRoomsPlaced = 0;

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
                // Preserve the completed main path before generating branches.
                List<DungeonRoom> mainPath = new(placedRooms);

                // Generate optional branches from available doorways.
                GenerateBranches(mainPath);

                Debug.Log(
                    $"Generated {placedRooms.Count} rooms, " +
                    $"including optional branches."
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
        /// Generates branches from available main-path doorways.
        /// </summary>
        private void GenerateBranches(List<DungeonRoom> mainPath)
        {
            foreach (DungeonRoom room in mainPath)
            {
                // Collect the room's unused doorways.
                List<DungeonDoor> availableDoors = new();

                foreach (DungeonDoor door in room.Doors)
                {
                    if (door != null && !door.IsConnected)
                        availableDoors.Add(door);
                }

                Shuffle(availableDoors);

                foreach (DungeonDoor door in availableDoors)
                {
                    if (branchRoomsPlaced >= maxBranchRooms)
                        return;

                    // Randomly decide whether to create a branch.
                    if (Random.value >= branchChance)
                        continue;

                    // Choose a random target length within our limits.
                    int branchLength = Random.Range(
                        minBranchLength,
                        Mathf.Max(minBranchLength, maxBranchLength) + 1
                    );

                    GenerateBranch(door, branchLength);
                }
            }
        }


        /// <summary>
        /// Recursively extends a branch, trying alternative doorways if blocked.
        /// </summary>
        private bool GenerateBranch(
            DungeonDoor targetDoor,
            int roomsRemaining)
        {
            // Stop when the branch is complete or we've reached our room limit.
            if (roomsRemaining <= 0 ||
                branchRoomsPlaced >= maxBranchRooms ||
                targetDoor == null ||
                targetDoor.IsConnected)
            {
                return false;
            }

            // Randomise our room prefabs.
            List<DungeonRoom> candidates = new(roomPrefabs);
            Shuffle(candidates);

            foreach (DungeonRoom prefab in candidates)
            {
                if (prefab == null)
                    continue;

                DungeonRoom candidate = Instantiate(
                    prefab,
                    Vector3.zero,
                    Quaternion.identity,
                    transform
                );

                // Try each doorway on the candidate.
                List<DungeonDoor> candidateDoors = new(candidate.Doors);
                Shuffle(candidateDoors);

                foreach (DungeonDoor candidateDoor in candidateDoors)
                {
                    if (candidateDoor == null)
                        continue;

                    // Attempt to position the candidate.
                    if (!roomPlacer.TryPlaceRoom(
                        candidate,
                        candidateDoor,
                        targetDoor,
                        placedRooms))
                    {
                        continue;
                    }

                    // Accept the placement.
                    candidateDoor.Connect();
                    targetDoor.Connect();

                    placedRooms.Add(candidate);
                    branchRoomsPlaced++;

                    // Collect the new room's available continuation doorways.
                    List<DungeonDoor> continuationDoors = new();

                    foreach (DungeonDoor door in candidate.Doors)
                    {
                        if (door != null && !door.IsConnected)
                            continuationDoors.Add(door);
                    }

                    Shuffle(continuationDoors);

                    // Try alternative directions until one successfully extends.
                    foreach (DungeonDoor continuationDoor in continuationDoors)
                    {
                        if (GenerateBranch(
                            continuationDoor,
                            roomsRemaining - 1))
                        {
                            break;
                        }
                    }

                    return true;
                }

                // No doorway worked for this candidate.
                Destroy(candidate.gameObject);
            }

            return false;
        }

        /// <summary>
        /// Attempts to attach a single branch room to an available doorway.
        /// </summary>
        private bool TryPlaceBranchRoom(DungeonDoor targetDoor)
        {
            // Randomise the available room prefabs.
            List<DungeonRoom> candidates = new(roomPrefabs);
            Shuffle(candidates);

            foreach (DungeonRoom prefab in candidates)
            {
                if (prefab == null)
                    continue;

                // Instantiate a candidate room.
                DungeonRoom candidate = Instantiate(
                    prefab,
                    Vector3.zero,
                    Quaternion.identity,
                    transform
                );

                // Try connecting each of its available doorways.
                List<DungeonDoor> candidateDoors = new(candidate.Doors);
                Shuffle(candidateDoors);

                foreach (DungeonDoor candidateDoor in candidateDoors)
                {
                    if (candidateDoor == null)
                        continue;

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
                    placedRooms.Add(candidate);

                    return true;
                }

                // Discard the candidate if none of its doorways fit.
                Destroy(candidate.gameObject);
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