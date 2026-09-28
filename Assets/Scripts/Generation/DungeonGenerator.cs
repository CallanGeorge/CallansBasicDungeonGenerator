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
        [SerializeField, Min(1)]
        private int maxBranchDepth = 2;

        [SerializeField, Range(0f, 1f)]
        private float childBranchChance = 0.4f;

        [SerializeField, Min(0)]
        private int maxChildBranchesPerBranch = 2;

        [Header("Debugging")]
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private GenerationRecorder recorder;

        [Header("Optimisation")]
        [SerializeField]
        private DungeonOpportunityDetector opportunityDetector;

        [SerializeField]
        private bool applyRoomUpgrades = true;

        private readonly List<DungeonRoom> placedRooms = new();
        private readonly RoomPlacer roomPlacer = new();

        private int attempts;
        private int branchRoomsPlaced;

        public IReadOnlyList<DungeonRoom> PlacedRooms => placedRooms;

        private void Start()
        {
            if (generateOnStart)
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
            recorder?.Clear();

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
            recorder?.RecordPlacement(firstRoom, startRoom);

            // Generate the remaining rooms.
            if (TryGeneratePath(firstRoom))
            {
                // Preserve the completed main path before generating branches.
                List<DungeonRoom> mainPath = new(placedRooms);

                // Generate optional branches from available doorways.
                GenerateBranches(mainPath);

                // Analyse and optimise the completed dungeon.
                if (opportunityDetector != null)
                {
                    DungeonRoom generatedStart = mainPath[0];
                    DungeonRoom generatedEnd = mainPath[mainPath.Count - 1];

                    opportunityDetector.Analyze(
                        placedRooms,
                        roomPrefabs,
                        generatedStart,
                        generatedEnd
                    );

                    if (applyRoomUpgrades)
                    {
                        opportunityDetector.ApplyUpgrades(placedRooms);
                    }
                }

                // Connect all remaining geometrically aligned doorways.
                int connectionsCreated = ConnectAlignedDoors();

                Debug.Log(
                    $"Created {connectionsCreated} automatic doorway connections."
                );

                // Refresh opportunity analysis after all modifications.
                if (opportunityDetector != null)
                {
                    opportunityDetector.Analyze(
                        placedRooms,
                        roomPrefabs,
                        mainPath[0],
                        mainPath[mainPath.Count - 1]
                    );
                }

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

                        // Establish the connection between both rooms.
                        if (!candidateDoor.Connect(targetDoor))
                            continue;

                        placedRooms.Add(candidate);
                        recorder?.RecordPlacement(candidate, prefab);

                        // Attempt to complete the remaining path.
                        if (TryGeneratePath(candidate))
                            return true;

                        // Backtrack if the remaining path cannot be completed.
                        recorder?.RecordRemoval(candidate);

                        placedRooms.Remove(candidate);

                        // Disconnecting one doorway clears both ends.
                        candidateDoor.Disconnect();

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

                    GenerateBranch(
                        door,
                        branchLength,
                        depth: 1
                    );
                }
            }
        }


        /// <summary>
        /// Generates a branch and optionally creates child branches from its rooms.
        /// </summary>
        private bool GenerateBranch(
            DungeonDoor targetDoor,
            int targetLength,
            int depth)
        {
            // Track the rooms belonging to this particular branch.
            List<DungeonRoom> branchRooms = new();

            // Generate the branch's rooms.
            bool success = TryExtendBranch(
                targetDoor,
                targetLength,
                branchRooms
            );

            if (!success)
                return false;

            // Attempt child branches if we haven't reached maximum depth.
            if (depth < maxBranchDepth)
                GenerateChildBranches(branchRooms, depth);

            return true;
        }

        /// <summary>
        /// Recursively extends a branch, trying alternative doorways if blocked.
        /// </summary>
        private bool TryExtendBranch(
            DungeonDoor targetDoor,
            int roomsRemaining,
            List<DungeonRoom> branchRooms)
        {
            // Stop if the branch is complete or its room budget is exhausted.
            if (roomsRemaining <= 0 ||
                branchRoomsPlaced >= maxBranchRooms ||
                targetDoor == null ||
                targetDoor.IsConnected)
            {
                return false;
            }

            // Randomise the available room prefabs.
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

                // Try the candidate's doorways in random order.
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

                    // Connect the new room to its parent.
                    if (!candidateDoor.Connect(targetDoor))
                        continue;

                    placedRooms.Add(candidate);
                    branchRooms.Add(candidate);
                    branchRoomsPlaced++;

                    recorder?.RecordPlacement(candidate, prefab);

                    // Collect available continuation doorways.
                    List<DungeonDoor> continuationDoors = new();

                    foreach (DungeonDoor door in candidate.Doors)
                    {
                        if (door != null && !door.IsConnected)
                            continuationDoors.Add(door);
                    }

                    Shuffle(continuationDoors);

                    // Try extending the branch through alternative doorways.
                    foreach (DungeonDoor continuationDoor in continuationDoors)
                    {
                        if (TryExtendBranch(
                            continuationDoor,
                            roomsRemaining - 1,
                            branchRooms))
                        {
                            break;
                        }
                    }

                    return true;
                }

                Destroy(candidate.gameObject);
            }

            return false;
        }

        /// <summary>
        /// Attempts to generate child branches from an existing branch's rooms.
        /// </summary>
        private void GenerateChildBranches(
            List<DungeonRoom> branchRooms,
            int parentDepth)
        {
            int childrenGenerated = 0;

            // Randomise the rooms belonging to this branch.
            List<DungeonRoom> candidates = new(branchRooms);
            Shuffle(candidates);

            foreach (DungeonRoom room in candidates)
            {
                // Stop when we've reached either generation limit.
                if (childrenGenerated >= maxChildBranchesPerBranch ||
                    branchRoomsPlaced >= maxBranchRooms)
                {
                    return;
                }

                // Collect available doorways on this branch room.
                List<DungeonDoor> availableDoors = new();

                foreach (DungeonDoor door in room.Doors)
                {
                    if (door != null && !door.IsConnected)
                        availableDoors.Add(door);
                }

                Shuffle(availableDoors);

                foreach (DungeonDoor door in availableDoors)
                {
                    if (childrenGenerated >= maxChildBranchesPerBranch ||
                        branchRoomsPlaced >= maxBranchRooms)
                    {
                        return;
                    }

                    // Randomly decide whether to attempt a child branch.
                    if (Random.value >= childBranchChance)
                        continue;

                    int length = Random.Range(
                        minBranchLength,
                        Mathf.Max(minBranchLength, maxBranchLength) + 1
                    );

                    // Generate the child at the next depth.
                    if (GenerateBranch(door, length, parentDepth + 1))
                        childrenGenerated++;
                }
            }
        }

        /// <summary>
        /// Connects unused doorways that already align geometrically.
        /// </summary>
        private int ConnectAlignedDoors()
        {
            int connectionsCreated = 0;

            // Compare every pair of different rooms.
            for (int i = 0; i < placedRooms.Count; i++)
            {
                DungeonRoom firstRoom = placedRooms[i];

                for (int j = i + 1; j < placedRooms.Count; j++)
                {
                    DungeonRoom secondRoom = placedRooms[j];

                    // Examine their unused doorways.
                    foreach (DungeonDoor firstDoor in firstRoom.Doors)
                    {
                        if (firstDoor == null || firstDoor.IsConnected)
                            continue;

                        foreach (DungeonDoor secondDoor in secondRoom.Doors)
                        {
                            if (!roomPlacer.DoorsAlign(
                                firstDoor,
                                secondDoor))
                            {
                                continue;
                            }

                            // Establish the connection.
                            if (firstDoor.Connect(secondDoor))
                            {
                                connectionsCreated++;
                                break;
                            }
                        }
                    }
                }
            }

            return connectionsCreated;
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