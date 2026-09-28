using System.Collections.Generic;
using UnityEngine;

namespace CallansDungeonGenerator
{
    public class RoomPlacer
    {
        /// <summary>
        /// Aligns two doorways so they share a position and face opposite directions.
        /// </summary>
        public bool AlignRooms(
            DungeonRoom newRoom,
            DungeonDoor newDoor,
            DungeonDoor targetDoor)
        {
            // Calculate the current and desired doorway directions.
            Vector3 currentDirection = newDoor.Forward;
            Vector3 desiredDirection = -targetDoor.Forward;

            // Only allow horizontal rotation.
            currentDirection.y = 0;
            desiredDirection.y = 0;

            // Reject invalid doorway directions.
            if (currentDirection.sqrMagnitude < 0.0001f ||
                desiredDirection.sqrMagnitude < 0.0001f)
                return false;

            // Calculate and apply the required rotation.
            float angle = Vector3.SignedAngle(
                currentDirection,
                desiredDirection,
                Vector3.up
            );

            newRoom.transform.Rotate(0, angle, 0, Space.World);

            // Move the room so both doorways occupy the same position.
            Vector3 offset = targetDoor.Position - newDoor.Position;
            newRoom.transform.position += offset;

            return true;
        }

        /// <summary>
        /// Returns true if a room overlaps any previously placed rooms.
        /// </summary>
        public bool OverlapsExistingRooms(
            DungeonRoom candidate,
            IReadOnlyList<DungeonRoom> existingRooms)
        {
            // Reject rooms without valid bounds.
            if (candidate == null || candidate.Bounds == null)
                return true;

            // Update physics after any recent Transform changes.
            Physics.SyncTransforms();

            // Shrink the bounds slightly to allow rooms to touch.
            Bounds candidateBounds = candidate.Bounds.bounds;
            candidateBounds.Expand(-0.02f);

            // Check the candidate against every existing room.
            foreach (DungeonRoom existingRoom in existingRooms)
            {
                if (existingRoom == null || existingRoom.Bounds == null)
                    continue;

                if (candidateBounds.Intersects(existingRoom.Bounds.bounds))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to align and place a room without overlapping existing rooms.
        /// Restores its original Transform if placement fails.
        /// </summary>
        public bool TryPlaceRoom(
            DungeonRoom newRoom,
            DungeonDoor newDoor,
            DungeonDoor targetDoor,
            IReadOnlyList<DungeonRoom> existingRooms)
        {
            // Validate references and ensure both doorways are available.
            if (newRoom == null ||
                newDoor == null ||
                targetDoor == null ||
                existingRooms == null)
                return false;

            if (newDoor.IsConnected || targetDoor.IsConnected)
                return false;

            // Save the original Transform in case placement fails.
            Vector3 originalPosition = newRoom.transform.position;
            Quaternion originalRotation = newRoom.transform.rotation;

            // Align the new room with the target doorway.
            if (!AlignRooms(newRoom, newDoor, targetDoor))
                return false;

            // Reject overlaps and restore the original Transform.
            if (OverlapsExistingRooms(newRoom, existingRooms))
            {
                newRoom.transform.SetPositionAndRotation(
                    originalPosition,
                    originalRotation
                );

                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks whether two unused doorways share a position
        /// and face opposite directions.
        /// </summary>
        public bool DoorsAlign(
            DungeonDoor firstDoor,
            DungeonDoor secondDoor,
            float positionTolerance = 0.05f,
            float angleTolerance = 5f)
        {
            // Reject missing or occupied doorways.
            if (firstDoor == null || secondDoor == null ||
                firstDoor.IsConnected || secondDoor.IsConnected)
                return false;

            // Check whether both doorways occupy the same position.
            float distance = Vector3.Distance(
                firstDoor.Position,
                secondDoor.Position
            );

            if (distance > positionTolerance)
                return false;

            // Check whether both doorways face opposite directions.
            float angle = Vector3.Angle(
                firstDoor.Forward,
                -secondDoor.Forward
            );

            return angle <= angleTolerance;
        }
    }
}