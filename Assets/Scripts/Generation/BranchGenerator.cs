using System.Collections.Generic;
using UnityEngine;

namespace CallansDungeonGenerator
{
    /// <summary>
    /// Generates branches and child branches from a completed main path.
    /// </summary>
    public class BranchGenerator
    {
        private readonly List<DungeonRoom> placedRooms;
        private readonly IReadOnlyList<DungeonRoom> roomPrefabs;
        private readonly RoomPlacer roomPlacer;
        private readonly GenerationRecorder recorder;
        private readonly Transform parent;

        private readonly float branchChance;
        private readonly int maxBranchRooms;
        private readonly int minBranchLength;
        private readonly int maxBranchLength;
        private readonly int maxBranchDepth;
        private readonly float childBranchChance;
        private readonly int maxChildBranchesPerBranch;

        private int branchRoomsPlaced;

        public BranchGenerator(
            List<DungeonRoom> placedRooms,
            IReadOnlyList<DungeonRoom> roomPrefabs,
            RoomPlacer roomPlacer,
            GenerationRecorder recorder,
            Transform parent,
            float branchChance,
            int maxBranchRooms,
            int minBranchLength,
            int maxBranchLength,
            int maxBranchDepth,
            float childBranchChance,
            int maxChildBranchesPerBranch)
        {
            this.placedRooms = placedRooms;
            this.roomPrefabs = roomPrefabs;
            this.roomPlacer = roomPlacer;
            this.recorder = recorder;
            this.parent = parent;
            this.branchChance = branchChance;
            this.maxBranchRooms = maxBranchRooms;
            this.minBranchLength = minBranchLength;
            this.maxBranchLength = maxBranchLength;
            this.maxBranchDepth = maxBranchDepth;
            this.childBranchChance = childBranchChance;
            this.maxChildBranchesPerBranch = maxChildBranchesPerBranch;
        }

        /// <summary>
        /// Generates branches from available main-path doorways.
        /// </summary>
        public void GenerateBranches(List<DungeonRoom> mainPath)
        {
            branchRoomsPlaced = 0;

            foreach (DungeonRoom room in mainPath)
            {
                // Collect the room's unused doorways.
                List<DungeonDoor> availableDoors =
                    GenerationUtility.GetAvailableDoors(room);

                GenerationUtility.Shuffle(availableDoors);

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

                    GenerateBranch(door, branchLength, depth: 1);
                }
            }
        }

        /// <summary>
        /// Generates a branch and its eligible child branches.
        /// </summary>
        private bool GenerateBranch(
            DungeonDoor targetDoor,
            int targetLength,
            int depth)
        {
            // Track the rooms belonging to this particular branch.
            List<DungeonRoom> branchRooms = new();

            // Generate the branch's rooms.
            if (!TryExtendBranch(targetDoor, targetLength, branchRooms))
                return false;

            // Attempt child branches if we haven't reached maximum depth.
            if (depth < maxBranchDepth)
                GenerateChildBranches(branchRooms, depth);

            return true;
        }

        /// <summary>
        /// Recursively extends a branch through available doorways.
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
            GenerationUtility.Shuffle(candidates);

            foreach (DungeonRoom prefab in candidates)
            {
                if (prefab == null)
                    continue;

                DungeonRoom candidate = Object.Instantiate(
                    prefab,
                    Vector3.zero,
                    Quaternion.identity,
                    parent
                );

                // Try the candidate's doorways in random order.
                List<DungeonDoor> candidateDoors = new(candidate.Doors);
                GenerationUtility.Shuffle(candidateDoors);

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

                    // Connect the new room to its parent.
                    if (!candidateDoor.Connect(targetDoor))
                        continue;

                    // Accept the placement and track this branch's rooms.
                    placedRooms.Add(candidate);
                    branchRooms.Add(candidate);
                    branchRoomsPlaced++;

                    recorder?.RecordPlacement(candidate, prefab);

                    // Collect available continuation doorways.
                    List<DungeonDoor> continuationDoors =
                        GenerationUtility.GetAvailableDoors(candidate);

                    GenerationUtility.Shuffle(continuationDoors);

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

                // No doorway worked for this candidate.
                Object.Destroy(candidate.gameObject);
            }

            return false;
        }

        /// <summary>
        /// Attempts child branches from the rooms in a completed branch.
        /// </summary>
        private void GenerateChildBranches(
            List<DungeonRoom> branchRooms,
            int parentDepth)
        {
            int childrenGenerated = 0;

            // Randomise the rooms belonging to this branch.
            List<DungeonRoom> candidates = new(branchRooms);
            GenerationUtility.Shuffle(candidates);

            foreach (DungeonRoom room in candidates)
            {
                // Stop when we've reached either generation limit.
                if (childrenGenerated >= maxChildBranchesPerBranch ||
                    branchRoomsPlaced >= maxBranchRooms)
                {
                    return;
                }

                // Collect available doorways on this branch room.
                List<DungeonDoor> availableDoors =
                    GenerationUtility.GetAvailableDoors(room);

                GenerationUtility.Shuffle(availableDoors);

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
    }
}
