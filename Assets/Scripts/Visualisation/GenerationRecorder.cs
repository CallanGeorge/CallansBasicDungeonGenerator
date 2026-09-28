using System.Collections.Generic;
using UnityEngine;

namespace CallansDungeonGenerator
{
    /// <summary>
    /// Records room placements and removals during dungeon generation.
    /// </summary>
    public class GenerationRecorder : MonoBehaviour
    {
        private readonly List<GenerationStep> steps = new();

        private readonly Dictionary<DungeonRoom, int> roomIds = new();

        private int nextRoomId;

        public IReadOnlyList<GenerationStep> Steps => steps;

        /// <summary>
        /// Clears all previously recorded generation events.
        /// </summary>
        public void Clear()
        {
            steps.Clear();
            roomIds.Clear();
            nextRoomId = 0;
        }

        /// <summary>
        /// Records the placement of a room.
        /// </summary>
        public void RecordPlacement(
            DungeonRoom room,
            DungeonRoom prefab)
        {
            if (room == null || prefab == null)
                return;

            // Assign a unique identifier to the generated room.
            int roomId = nextRoomId++;

            roomIds.Add(room, roomId);

            // Record the room's final position and rotation.
            steps.Add(new GenerationStep(
                GenerationStepType.RoomPlaced,
                roomId,
                prefab,
                room.transform.position,
                room.transform.rotation
            ));
        }

        /// <summary>
        /// Records the removal of a previously placed room.
        /// </summary>
        public void RecordRemoval(DungeonRoom room)
        {
            if (!roomIds.TryGetValue(room, out int roomId))
                return;

            // Record the removal using the room's original identifier.
            steps.Add(new GenerationStep(
                GenerationStepType.RoomRemoved,
                roomId,
                null,
                Vector3.zero,
                Quaternion.identity
            ));

            roomIds.Remove(room);
        }
    }
}
