using System.Collections.Generic;
using UnityEngine;

namespace CallansDungeonGenerator
{
    /// <summary>
    /// Shared collection helpers used by generation algorithms.
    /// </summary>
    internal static class GenerationUtility
    {
        /// <summary>
        /// Collects the unused doorways belonging to a room.
        /// </summary>
        public static List<DungeonDoor> GetAvailableDoors(DungeonRoom room)
        {
            List<DungeonDoor> availableDoors = new();

            foreach (DungeonDoor door in room.Doors)
            {
                if (door != null && !door.IsConnected)
                    availableDoors.Add(door);
            }

            return availableDoors;
        }

        /// <summary>
        /// Randomises the order of elements in a list.
        /// </summary>
        public static void Shuffle<T>(List<T> items)
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
