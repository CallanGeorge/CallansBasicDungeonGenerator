using System.Collections.Generic;
using UnityEngine;

namespace CallansDungeonGenerator
{
    /// <summary>
    /// Generates the dungeon's main path using recursive backtracking.
    /// </summary>
    public class MainPathGenerator
    {
        private readonly List<DungeonRoom> placedRooms;
        private readonly IReadOnlyList<DungeonRoom> roomPrefabs;
        private readonly DungeonRoom endRoom;
        private readonly RoomPlacer roomPlacer;
        private readonly GenerationRecorder recorder;
        private readonly Transform parent;
        private readonly int roomCount;
        private readonly int maxAttempts;

        public int Attempts { get; private set; }

        public MainPathGenerator(
            List<DungeonRoom> placedRooms,
            IReadOnlyList<DungeonRoom> roomPrefabs,
            DungeonRoom endRoom,
            RoomPlacer roomPlacer,
            GenerationRecorder recorder,
            Transform parent,
            int roomCount,
            int maxAttempts)
        {
            this.placedRooms = placedRooms;
            this.roomPrefabs = roomPrefabs;
            this.endRoom = endRoom;
            this.roomPlacer = roomPlacer;
            this.recorder = recorder;
            this.parent = parent;
            this.roomCount = roomCount;
            this.maxAttempts = maxAttempts;
        }

        /// <summary>
        /// Extends the main path from the supplied starting room.
        /// </summary>
        public bool Generate(DungeonRoom firstRoom)
        {
            Attempts = 0;
            return TryGeneratePath(firstRoom);
        }

        /// <summary>
        /// Recursively extends the main path and backtracks when necessary.
        /// </summary>
        private bool TryGeneratePath(DungeonRoom previousRoom)
        {
            // Stop when we've generated the requested number of rooms.
            if (placedRooms.Count >= roomCount)
                return true;

            if (Attempts >= maxAttempts)
                return false;

            // Collect and randomise the available doorways.
            List<DungeonDoor> availableDoors =
                GenerationUtility.GetAvailableDoors(previousRoom);

            GenerationUtility.Shuffle(availableDoors);

            foreach (DungeonDoor targetDoor in availableDoors)
            {
                // Use the ending room when placing the final room.
                bool isLastRoom = placedRooms.Count == roomCount - 1;

                List<DungeonRoom> candidates = isLastRoom
                    ? new List<DungeonRoom> { endRoom }
                    : new List<DungeonRoom>(roomPrefabs);

                GenerationUtility.Shuffle(candidates);

                foreach (DungeonRoom prefab in candidates)
                {
                    if (Attempts >= maxAttempts)
                        return false;

                    if (prefab == null)
                        continue;

                    // Instantiate a candidate and randomise its doorways.
                    DungeonRoom candidate = Object.Instantiate(
                        prefab,
                        Vector3.zero,
                        Quaternion.identity,
                        parent
                    );

                    List<DungeonDoor> candidateDoors =
                        new(candidate.Doors);

                    GenerationUtility.Shuffle(candidateDoors);

                    foreach (DungeonDoor candidateDoor in candidateDoors)
                    {
                        if (Attempts >= maxAttempts)
                            break;

                        if (candidateDoor == null)
                            continue;

                        Attempts++;

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
                    Object.Destroy(candidate.gameObject);

                    if (Attempts >= maxAttempts)
                        return false;
                }
            }

            return false;
        }
    }
}
