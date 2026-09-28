using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CallansDungeonGenerator
{
    /// <summary>
    /// Replays recorded dungeon generation events with configurable delays.
    /// </summary>
    public class DungeonVisualiser : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DungeonGenerator generator;
        [SerializeField] private GenerationRecorder recorder;

        [Header("Visualisation Settings")]
        [SerializeField, Min(0f)] private float stepDelay = 0.25f;

        private readonly Dictionary<int, GameObject> visualRooms = new();

        /// <summary>
        /// Generates a dungeon and replays its recorded generation steps.
        /// </summary>
        private IEnumerator Start()
        {
            if (generator == null || recorder == null)
            {
                Debug.LogError("Missing generator or recorder reference.");
                yield break;
            }

            // Generate the dungeon normally.
            if (!generator.Generate())
                yield break;

            // Hide the completed dungeon during playback.
            List<Renderer> hiddenRenderers = new();
            List<bool> originalStates = new();

            foreach (DungeonRoom room in generator.PlacedRooms)
            {
                foreach (Renderer renderer in
                    room.GetComponentsInChildren<Renderer>(true))
                {
                    hiddenRenderers.Add(renderer);
                    originalStates.Add(renderer.enabled);
                    renderer.enabled = false;
                }
            }

            // Replay each recorded generation event.
            foreach (GenerationStep step in recorder.Steps)
            {
                switch (step.Type)
                {
                    case GenerationStepType.RoomPlaced:
                        ShowRoom(step);
                        break;

                    case GenerationStepType.RoomRemoved:
                        RemoveRoom(step.RoomId);
                        break;
                }

                yield return new WaitForSecondsRealtime(stepDelay);
            }

            // Remove the visual copies after playback.
            foreach (GameObject room in visualRooms.Values)
                Destroy(room);

            visualRooms.Clear();

            // Reveal the actual generated dungeon.
            for (int i = 0; i < hiddenRenderers.Count; i++)
            {
                if (hiddenRenderers[i] != null)
                    hiddenRenderers[i].enabled = originalStates[i];
            }

            Debug.Log("Dungeon visualisation complete.");
        }

        /// <summary>
        /// Creates a visual copy of a recorded room placement.
        /// </summary>
        private void ShowRoom(GenerationStep step)
        {
            if (step.Prefab == null)
                return;

            // Instantiate the recorded room at its original position.
            GameObject room = Instantiate(
                step.Prefab.gameObject,
                step.Position,
                step.Rotation,
                transform
            );

            room.name = $"Visual Room {step.RoomId}";

            // Disable physics on visual copies.
            foreach (Collider collider in
                room.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (Rigidbody body in
                room.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }

            // Disable gameplay scripts on visual copies.
            foreach (MonoBehaviour behaviour in
                room.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }

            visualRooms.Add(step.RoomId, room);
        }

        /// <summary>
        /// Removes the visual copy of a backtracked room.
        /// </summary>
        private void RemoveRoom(int roomId)
        {
            if (!visualRooms.TryGetValue(roomId, out GameObject room))
                return;

            Destroy(room);
            visualRooms.Remove(roomId);
        }
    }
}
