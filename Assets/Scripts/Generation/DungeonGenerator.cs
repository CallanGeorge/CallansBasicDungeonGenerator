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

        [Header("Generation Control")]
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private GenerationRecorder recorder;

        [Header("Optimisation")]
        [SerializeField]
        private bool applyRoomUpgrades = true;

        [Header("Doorway Closures")]
        [SerializeField] private GameObject doorClosurePrefab;

        private readonly List<DungeonRoom> placedRooms = new();
        private readonly RoomPlacer roomPlacer = new();

        public IReadOnlyList<DungeonRoom> PlacedRooms => placedRooms;

        private void Start()
        {
            if (generateOnStart)
                Generate();
        }

        /// <summary>
        /// Generates a complete dungeon.
        /// </summary>
        public bool Generate()
        {
            // Validate the generator's serialized configuration.
            if (!HasValidConfiguration())
            {
                Debug.LogError("Invalid dungeon configuration.");
                return false;
            }

            // Remove the previous dungeon and reset recorded events.
            ResetGeneration();

            // Generate the main path from a newly instantiated start room.
            DungeonRoom firstRoom = CreateStartingRoom();
            MainPathGenerator mainPathGenerator = CreateMainPathGenerator();

            if (!mainPathGenerator.Generate(firstRoom))
                return HandleGenerationFailure(mainPathGenerator.Attempts);

            // Preserve the completed main path before generating branches.
            List<DungeonRoom> mainPath = new(placedRooms);

            // Generate branches, optimise the layout and close unused doors.
            CreateBranchGenerator().GenerateBranches(mainPath);
            OptimiseDungeon(mainPath);
            CloseUnusedDoors();

            Debug.Log(
                $"Generated {placedRooms.Count} rooms, " +
                $"including optional branches."
            );

            return true;
        }

        /// <summary>
        /// Returns true when the required generation settings are valid.
        /// </summary>
        private bool HasValidConfiguration()
        {
            return startRoom != null &&
                endRoom != null &&
                roomPrefabs != null &&
                roomPrefabs.Count > 0 &&
                roomCount >= 2 &&
                maxAttempts >= 1;
        }

        /// <summary>
        /// Clears all state left by the previous generation pass.
        /// </summary>
        private void ResetGeneration()
        {
            Clear();
            recorder?.Clear();
        }

        /// <summary>
        /// Instantiates and records the dungeon's starting room.
        /// </summary>
        private DungeonRoom CreateStartingRoom()
        {
            DungeonRoom firstRoom = Instantiate(
                startRoom,
                transform.position,
                Quaternion.identity,
                transform
            );

            placedRooms.Add(firstRoom);
            recorder?.RecordPlacement(firstRoom, startRoom);

            return firstRoom;
        }

        /// <summary>
        /// Creates the main-path generator with the shared generation state.
        /// </summary>
        private MainPathGenerator CreateMainPathGenerator()
        {
            return new MainPathGenerator(
                placedRooms,
                roomPrefabs,
                endRoom,
                roomPlacer,
                recorder,
                transform,
                roomCount,
                maxAttempts
            );
        }

        /// <summary>
        /// Creates the branch generator with the configured branch limits.
        /// </summary>
        private BranchGenerator CreateBranchGenerator()
        {
            return new BranchGenerator(
                placedRooms,
                roomPrefabs,
                roomPlacer,
                recorder,
                transform,
                branchChance,
                maxBranchRooms,
                minBranchLength,
                maxBranchLength,
                maxBranchDepth,
                childBranchChance,
                maxChildBranchesPerBranch
            );
        }

        /// <summary>
        /// Applies room upgrades and connects geometrically aligned doorways.
        /// </summary>
        private void OptimiseDungeon(List<DungeonRoom> mainPath)
        {
            // Special rooms are identified from the preserved main path.
            DungeonRoom generatedStart = mainPath[0];
            DungeonRoom generatedEnd = mainPath[mainPath.Count - 1];

            DungeonOptimiser optimiser = new(
                roomPrefabs,
                roomPlacer,
                transform
            );

            // Detect possible replacements before modifying the dungeon.
            optimiser.Analyze(
                placedRooms,
                generatedStart,
                generatedEnd
            );

            if (applyRoomUpgrades)
                optimiser.ApplyUpgrades(placedRooms);

            // Connect all remaining geometrically aligned doorways.
            int connectionsCreated =
                optimiser.ConnectAlignedDoors(placedRooms);

            Debug.Log(
                $"Created {connectionsCreated} automatic doorway connections."
            );

            // Refresh analysis so it represents the completed dungeon.
            optimiser.Analyze(
                placedRooms,
                generatedStart,
                generatedEnd
            );
        }

        /// <summary>
        /// Reports unsuccessful generation and removes partial rooms.
        /// </summary>
        private bool HandleGenerationFailure(int attempts)
        {
            Debug.LogWarning(
                $"Dungeon generation failed after {attempts} attempts."
            );

            Clear();
            return false;
        }

        /// <summary>
        /// Closes every unused doorway in the completed dungeon.
        /// </summary>
        private void CloseUnusedDoors()
        {
            if (doorClosurePrefab == null)
                return;

            // Examine every doorway in the completed dungeon.
            foreach (DungeonRoom room in placedRooms)
            {
                foreach (DungeonDoor door in room.Doors)
                {
                    if (door == null || door.IsConnected)
                        continue;

                    // Instantiate the closure using the doorway's transform.
                    Instantiate(
                        doorClosurePrefab,
                        door.Position,
                        door.transform.rotation,
                        door.transform
                    );
                }
            }
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
    }
}