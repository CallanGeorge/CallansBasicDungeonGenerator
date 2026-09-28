using System.Collections.Generic;
using UnityEngine;

namespace CallansDungeonGenerator
{
    /// <summary>
    /// Detects and applies room upgrades and connects aligned doorways.
    /// </summary>
    public class DungeonOptimiser
    {
        private readonly IReadOnlyList<DungeonRoom> roomPrefabs;
        private readonly RoomPlacer roomPlacer;
        private readonly Transform parent;
        private readonly List<UpgradeProposal> upgradeProposals = new();

        public DungeonOptimiser(
            IReadOnlyList<DungeonRoom> roomPrefabs,
            RoomPlacer roomPlacer,
            Transform parent)
        {
            this.roomPrefabs = roomPrefabs;
            this.roomPlacer = roomPlacer;
            this.parent = parent;
        }

        /// <summary>
        /// Detects possible room upgrades in the current dungeon.
        /// </summary>
        public void Analyze(
            IReadOnlyList<DungeonRoom> rooms,
            DungeonRoom startRoom,
            DungeonRoom endRoom)
        {
            // Reset proposals from the previous analysis pass.
            upgradeProposals.Clear();

            if (rooms == null || roomPrefabs == null)
                return;

            foreach (DungeonRoom room in rooms)
            {
                // Never replace our special rooms.
                if (room == startRoom || room == endRoom)
                    continue;

                UpgradeProposal proposal = FindUpgrade(room, rooms);

                if (proposal != null)
                    upgradeProposals.Add(proposal);
            }

            Debug.Log(
                $"DUNGEON OPPORTUNITY REPORT\n" +
                $"Total rooms: {rooms.Count}\n" +
                $"Upgradeable rooms: {upgradeProposals.Count}"
            );
        }

        /// <summary>
        /// Applies the currently detected upgrades after revalidating each one.
        /// </summary>
        public int ApplyUpgrades(List<DungeonRoom> rooms)
        {
            int appliedUpgrades = 0;

            // Copy proposals because we'll modify the dungeon.
            List<UpgradeProposal> proposals = new(upgradeProposals);

            foreach (UpgradeProposal proposal in proposals)
            {
                DungeonRoom original = proposal.OriginalRoom;

                // Skip proposals referencing rooms that no longer exist.
                if (original == null || !rooms.Contains(original))
                    continue;

                // The additional connection must still be available.
                if (proposal.TargetDoor == null ||
                    proposal.TargetDoor.IsConnected)
                {
                    continue;
                }

                // Create the proposed replacement.
                DungeonRoom replacement = Object.Instantiate(
                    proposal.ReplacementPrefab,
                    proposal.Position,
                    proposal.Rotation,
                    original.transform.parent
                );

                // Validate the replacement against the current dungeon.
                if (!ValidateReplacement(
                    original,
                    replacement,
                    proposal.TargetDoor,
                    rooms,
                    out Dictionary<DungeonDoor, DungeonDoor>
                        preservedConnections,
                    out DungeonDoor additionalDoor))
                {
                    replacement.gameObject.SetActive(false);
                    Object.Destroy(replacement.gameObject);
                    continue;
                }

                // Transfer existing connections to the replacement room.
                foreach (var connection in preservedConnections)
                {
                    DungeonDoor originalDoor = connection.Key;
                    DungeonDoor replacementDoor = connection.Value;

                    // Remember the original doorway's partner.
                    DungeonDoor neighbourDoor = originalDoor.ConnectedDoor;

                    // Remove the old connection.
                    originalDoor.Disconnect();

                    // Establish the replacement connection.
                    replacementDoor.Connect(neighbourDoor);
                }

                // Establish the additional connection.
                additionalDoor.Connect(proposal.TargetDoor);

                // Replace the original room in our dungeon collection.
                int index = rooms.IndexOf(original);
                rooms[index] = replacement;

                original.gameObject.SetActive(false);
                Object.Destroy(original.gameObject);

                appliedUpgrades++;

                Debug.Log(
                    $"UPGRADE APPLIED: " +
                    $"{original.name} -> {proposal.ReplacementPrefab.name}"
                );
            }

            Debug.Log($"Successfully applied {appliedUpgrades} room upgrades.");

            return appliedUpgrades;
        }

        /// <summary>
        /// Connects every pair of unused doorways that already align.
        /// </summary>
        public int ConnectAlignedDoors(List<DungeonRoom> rooms)
        {
            int connectionsCreated = 0;

            // Compare every pair of different rooms.
            for (int i = 0; i < rooms.Count; i++)
            {
                DungeonRoom firstRoom = rooms[i];

                for (int j = i + 1; j < rooms.Count; j++)
                {
                    DungeonRoom secondRoom = rooms[j];

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
        /// Finds a replacement that preserves existing connections
        /// and introduces an additional connection.
        /// </summary>
        private UpgradeProposal FindUpgrade(
            DungeonRoom original,
            IReadOnlyList<DungeonRoom> rooms)
        {
            if (original == null || original.Bounds == null)
                return null;

            // Collect the connections that must be preserved.
            List<DungeonDoor> connectedDoors = new();

            foreach (DungeonDoor door in original.Doors)
            {
                if (door != null && door.IsConnected)
                    connectedDoors.Add(door);
            }

            if (connectedDoors.Count == 0)
                return null;

            // Exclude the original room from collision checks.
            List<DungeonRoom> otherRooms = new(rooms);
            otherRooms.Remove(original);

            foreach (DungeonRoom prefab in roomPrefabs)
            {
                if (prefab == null)
                    continue;

                // Create a temporary replacement.
                DungeonRoom candidate = Object.Instantiate(
                    prefab,
                    Vector3.zero,
                    Quaternion.identity,
                    parent
                );

                // Hide the candidate while evaluating it.
                foreach (Renderer renderer in
                         candidate.GetComponentsInChildren<Renderer>())
                {
                    renderer.enabled = false;
                }

                UpgradeProposal proposal = null;

                foreach (DungeonDoor candidateDoor in candidate.Doors)
                {
                    if (candidateDoor == null)
                        continue;

                    // Position the candidate using an existing connection.
                    AlignReplacement(
                        candidate,
                        candidateDoor,
                        connectedDoors[0]
                    );

                    // Every existing connection must be preserved.
                    if (!PreservesConnections(
                        candidate,
                        connectedDoors,
                        out HashSet<DungeonDoor> matchedDoors))
                    {
                        continue;
                    }

                    // Reject replacements that overlap other rooms.
                    if (roomPlacer.OverlapsExistingRooms(
                        candidate,
                        otherRooms))
                    {
                        continue;
                    }

                    // Check whether we can create another connection.
                    proposal = FindAdditionalConnection(
                        original,
                        prefab,
                        candidate,
                        matchedDoors,
                        otherRooms
                    );

                    if (proposal != null)
                        break;
                }

                // Remove our temporary evaluation object.
                candidate.gameObject.SetActive(false);
                Object.Destroy(candidate.gameObject);

                if (proposal != null)
                    return proposal;
            }

            return null;
        }

        /// <summary>
        /// Finds an additional valid connection created by a replacement.
        /// </summary>
        private UpgradeProposal FindAdditionalConnection(
            DungeonRoom original,
            DungeonRoom prefab,
            DungeonRoom candidate,
            HashSet<DungeonDoor> matchedDoors,
            List<DungeonRoom> otherRooms)
        {
            foreach (DungeonDoor newDoor in candidate.Doors)
            {
                // Ignore doorways preserving existing connections.
                if (newDoor == null || matchedDoors.Contains(newDoor))
                    continue;

                foreach (DungeonRoom otherRoom in otherRooms)
                {
                    foreach (DungeonDoor targetDoor in otherRoom.Doors)
                    {
                        if (!roomPlacer.DoorsAlign(newDoor, targetDoor))
                            continue;

                        // Save the replacement's exact configuration.
                        Physics.SyncTransforms();

                        return new UpgradeProposal(
                            original,
                            prefab,
                            candidate.transform.position,
                            candidate.transform.rotation,
                            targetDoor
                        );
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Positions a replacement so one doorway matches an existing doorway.
        /// </summary>
        private static void AlignReplacement(
            DungeonRoom candidate,
            DungeonDoor candidateDoor,
            DungeonDoor originalDoor)
        {
            // Reset the replacement before evaluating another orientation.
            candidate.transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.identity
            );

            // Replacement doorways must face the same direction.
            Vector3 current = candidateDoor.Forward;
            Vector3 desired = originalDoor.Forward;

            current.y = 0;
            desired.y = 0;

            float angle = Vector3.SignedAngle(
                current,
                desired,
                Vector3.up
            );

            candidate.transform.Rotate(0, angle, 0, Space.World);

            // Position the replacement's doorway over the original.
            candidate.transform.position +=
                originalDoor.Position - candidateDoor.Position;
        }

        /// <summary>
        /// Verifies that a replacement preserves every existing connection.
        /// </summary>
        private static bool PreservesConnections(
            DungeonRoom candidate,
            List<DungeonDoor> originalDoors,
            out HashSet<DungeonDoor> matchedDoors)
        {
            matchedDoors = new();

            foreach (DungeonDoor originalDoor in originalDoors)
            {
                bool foundMatch = false;

                foreach (DungeonDoor candidateDoor in candidate.Doors)
                {
                    if (candidateDoor == null ||
                        matchedDoors.Contains(candidateDoor))
                    {
                        continue;
                    }

                    float distance = Vector3.Distance(
                        originalDoor.Position,
                        candidateDoor.Position
                    );

                    float angle = Vector3.Angle(
                        originalDoor.Forward,
                        candidateDoor.Forward
                    );

                    if (distance > 0.05f || angle > 5f)
                        continue;

                    matchedDoors.Add(candidateDoor);
                    foundMatch = true;
                    break;
                }

                if (!foundMatch)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Validates a replacement and identifies its doorway connections.
        /// </summary>
        private bool ValidateReplacement(
            DungeonRoom original,
            DungeonRoom replacement,
            DungeonDoor targetDoor,
            List<DungeonRoom> rooms,
            out Dictionary<DungeonDoor, DungeonDoor> preservedConnections,
            out DungeonDoor additionalDoor)
        {
            preservedConnections = new();
            additionalDoor = null;

            if (original == null ||
                replacement == null ||
                targetDoor == null ||
                targetDoor.IsConnected ||
                replacement.Bounds == null)
            {
                return false;
            }

            // Identify the original room's connected doorways.
            foreach (DungeonDoor originalDoor in original.Doors)
            {
                if (originalDoor == null || !originalDoor.IsConnected)
                    continue;

                bool foundMatch = false;

                foreach (DungeonDoor replacementDoor in replacement.Doors)
                {
                    if (replacementDoor == null ||
                        preservedConnections.ContainsValue(replacementDoor))
                    {
                        continue;
                    }

                    float distance = Vector3.Distance(
                        originalDoor.Position,
                        replacementDoor.Position
                    );

                    float angle = Vector3.Angle(
                        originalDoor.Forward,
                        replacementDoor.Forward
                    );

                    if (distance > 0.05f || angle > 5f)
                        continue;

                    // Remember which replacement doorway corresponds
                    // to this original doorway.
                    preservedConnections.Add(
                        originalDoor,
                        replacementDoor
                    );

                    foundMatch = true;
                    break;
                }

                if (!foundMatch)
                    return false;
            }

            // Validate the replacement's bounds.
            List<DungeonRoom> otherRooms = new(rooms);
            otherRooms.Remove(original);

            if (roomPlacer.OverlapsExistingRooms(replacement, otherRooms))
                return false;

            // Find the doorway providing our additional connection.
            foreach (DungeonDoor door in replacement.Doors)
            {
                if (door == null ||
                    preservedConnections.ContainsValue(door))
                {
                    continue;
                }

                if (!roomPlacer.DoorsAlign(door, targetDoor))
                    continue;

                additionalDoor = door;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Describes a possible replacement for an existing room.
    /// </summary>
    internal class UpgradeProposal
    {
        public DungeonRoom OriginalRoom { get; }
        public DungeonRoom ReplacementPrefab { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public DungeonDoor TargetDoor { get; }

        public UpgradeProposal(
            DungeonRoom originalRoom,
            DungeonRoom replacementPrefab,
            Vector3 position,
            Quaternion rotation,
            DungeonDoor targetDoor)
        {
            OriginalRoom = originalRoom;
            ReplacementPrefab = replacementPrefab;
            Position = position;
            Rotation = rotation;
            TargetDoor = targetDoor;
        }
    }
}
