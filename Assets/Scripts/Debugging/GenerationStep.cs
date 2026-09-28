using UnityEngine;

namespace CallansDungeonGenerator
{
    public enum GenerationStepType
    {
        RoomPlaced,
        RoomRemoved
    }

    /// <summary>
    /// Stores the information needed to replay a generation event.
    /// </summary>
    public class GenerationStep
    {
        public GenerationStepType Type { get; }
        public int RoomId { get; }

        public DungeonRoom Prefab { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }

        public GenerationStep(
            GenerationStepType type,
            int roomId,
            DungeonRoom prefab,
            Vector3 position,
            Quaternion rotation)
        {
            Type = type;
            RoomId = roomId;
            Prefab = prefab;
            Position = position;
            Rotation = rotation;
        }
    }
}
