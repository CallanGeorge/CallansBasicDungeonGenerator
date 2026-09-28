using System.Collections.Generic;
using UnityEngine;

namespace CallansDungeonGenerator
{
    /// <summary>
    /// Identifies possible loop connections in a completed dungeon.
    /// </summary>
    public class DungeonOpportunityDetector : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField, Min(0.1f)]
        private float nearbyDistance = 12f;

        [SerializeField]
        private bool showGizmos = true;

        private readonly RoomPlacer roomPlacer = new();

        private readonly List<(Vector3, Vector3)> directConnections = new();
        private readonly List<(Vector3, Vector3)> upgradeConnections = new();
        private readonly List<(Vector3, Vector3)> nearbyConnections = new();

        [Header("Upgrade Preview")]
        [SerializeField]
        private bool showUpgradePreviews = true;

        [SerializeField]
        private bool showUpgradeConnections = true;

        private readonly List<UpgradeProposal> upgradeProposals = new();

        public IReadOnlyList<UpgradeProposal> UpgradeProposals
            => upgradeProposals;

        /// <summary>
        /// Analyses a completed dungeon for potential loop connections.
        /// </summary>
        public void Analyze(
            IReadOnlyList<DungeonRoom> rooms,
            IReadOnlyList<DungeonRoom> prefabs,
            DungeonRoom startRoom,
            DungeonRoom endRoom)
        {
            // Reset previous results.
            directConnections.Clear();
            upgradeConnections.Clear();
            nearbyConnections.Clear();
            upgradeProposals.Clear();

            if (rooms == null || prefabs == null)
                return;

            // Find existing doorway opportunities.
            FindDoorwayOpportunities(rooms);

            // Evaluate potential room upgrades.
            foreach (DungeonRoom room in rooms)
            {
                // Never replace our special rooms.
                if (room == startRoom || room == endRoom)
                    continue;

                UpgradeProposal proposal = FindUpgrade(
                    room,
                    rooms,
                    prefabs
                );

                if (proposal != null)
                {
                    upgradeProposals.Add(proposal);

                    upgradeConnections.Add((
                        proposal.NewConnectionPosition,
                        proposal.TargetDoor.Position
                    ));
                }
            }

            // Report our findings.
            Debug.Log(
                $"DUNGEON OPPORTUNITY REPORT\n" +
                $"Total rooms: {rooms.Count}\n" +
                $"Direct connections: {directConnections.Count}\n" +
                $"Upgradeable rooms: {upgradeProposals.Count}\n" +
                $"Nearby doorway pairs: {nearbyConnections.Count}"
            );

            // Report individual upgrade proposals.
            foreach (UpgradeProposal proposal in upgradeProposals)
            {
                Debug.Log(
                    $"UPGRADE PROPOSAL\n" +
                    $"Original: {proposal.OriginalRoom.name}\n" +
                    $"Replacement: {proposal.ReplacementPrefab.name}\n" +
                    $"Position: {proposal.Position}\n" +
                    $"Connects to: {proposal.TargetDoor.name}"
                );
            }
        }

        /// <summary>
        /// Applies valid room upgrades to an existing dungeon.
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
                DungeonRoom replacement = Instantiate(
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
                    Destroy(replacement.gameObject);

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
                Destroy(original.gameObject);

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

            if (roomPlacer.OverlapsExistingRooms(
                replacement,
                otherRooms))
            {
                return false;
            }

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

        /// <summary>
        /// Finds existing doorway pairs that might form connections.
        /// </summary>
        private void FindDoorwayOpportunities(
            IReadOnlyList<DungeonRoom> rooms)
        {
            // Compare each pair of rooms exactly once.
            for (int i = 0; i < rooms.Count; i++)
            {
                for (int j = i + 1; j < rooms.Count; j++)
                {
                    foreach (DungeonDoor first in rooms[i].Doors)
                    {
                        if (first == null || first.IsConnected)
                            continue;

                        foreach (DungeonDoor second in rooms[j].Doors)
                        {
                            if (second == null || second.IsConnected)
                                continue;

                            // Record doorways that already align.
                            if (roomPlacer.DoorsAlign(first, second))
                            {
                                directConnections.Add((
                                    first.Position,
                                    second.Position
                                ));

                                continue;
                            }

                            // Identify nearby doorways facing opposite directions.
                            float distance = Vector3.Distance(
                                first.Position,
                                second.Position
                            );

                            float angle = Vector3.Angle(
                                first.Forward,
                                -second.Forward
                            );

                            if (distance <= nearbyDistance && angle <= 5f)
                            {
                                nearbyConnections.Add((
                                    first.Position,
                                    second.Position
                                ));
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Finds a replacement that preserves existing connections
        /// and introduces an additional connection.
        /// </summary>
        private UpgradeProposal FindUpgrade(
            DungeonRoom original,
            IReadOnlyList<DungeonRoom> rooms,
            IReadOnlyList<DungeonRoom> prefabs)
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

            foreach (DungeonRoom prefab in prefabs)
            {
                if (prefab == null)
                    continue;

                // Create a temporary replacement.
                DungeonRoom candidate = Instantiate(
                    prefab,
                    Vector3.zero,
                    Quaternion.identity,
                    transform
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
                Destroy(candidate.gameObject);

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
                        if (!roomPlacer.DoorsAlign(
                            newDoor,
                            targetDoor))
                        {
                            continue;
                        }

                        // Save the replacement's exact configuration.
                        Physics.SyncTransforms();

                        return new UpgradeProposal(
                            original,
                            prefab,
                            candidate.transform.position,
                            candidate.transform.rotation,
                            candidate.Bounds.bounds,
                            newDoor.Position,
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
        private void AlignReplacement(
            DungeonRoom candidate,
            DungeonDoor candidateDoor,
            DungeonDoor originalDoor)
        {
            // Reset the replacement before evaluating another orientation.
            candidate.transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.identity
            );

            // Replacement doorways must face the SAME direction.
            Vector3 current = candidateDoor.Forward;
            Vector3 desired = originalDoor.Forward;

            current.y = 0;
            desired.y = 0;

            float angle = Vector3.SignedAngle(
                current,
                desired,
                Vector3.up
            );

            candidate.transform.Rotate(
                0,
                angle,
                0,
                Space.World
            );

            // Position the replacement's doorway over the original.
            candidate.transform.position +=
                originalDoor.Position - candidateDoor.Position;
        }

        /// <summary>
        /// Verifies that a replacement preserves every existing connection.
        /// </summary>
        private bool PreservesConnections(
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
                    if (matchedDoors.Contains(candidateDoor))
                        continue;

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
        /// Draws proposed room replacements and their new connections.
        /// </summary>
        private void DrawUpgradePreviews()
        {
            foreach (UpgradeProposal proposal in upgradeProposals)
            {
                if (proposal.OriginalRoom == null)
                    continue;

                // Draw the proposed replacement's bounds.
                if (showUpgradePreviews)
                {
                    Gizmos.color = Color.yellow;

                    Gizmos.DrawWireCube(
                        proposal.BoundsCenter,
                        proposal.BoundsSize
                    );

                    // Highlight the room being replaced.
                    Gizmos.color = Color.magenta;

                    Gizmos.DrawSphere(
                        proposal.OriginalRoom.transform.position,
                        0.5f
                    );
                }

                // Draw the additional connection.
                if (showUpgradeConnections &&
                    proposal.TargetDoor != null)
                {
                    Gizmos.color = Color.magenta;

                    Gizmos.DrawLine(
                        proposal.NewConnectionPosition,
                        proposal.TargetDoor.Position
                    );

                    Gizmos.DrawSphere(
                        proposal.NewConnectionPosition,
                        0.35f
                    );
                }
            }
        }

        /// <summary>
        /// Draws detected opportunities in the Scene view.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!showGizmos)
                return;

            // Direct connections.
            Gizmos.color = Color.green;

            foreach (var connection in directConnections)
            {
                Gizmos.DrawSphere(connection.Item1, 0.3f);
                Gizmos.DrawSphere(connection.Item2, 0.3f);
            }

            // Connections made possible by room upgrades.
            DrawUpgradePreviews();

            foreach (var connection in upgradeConnections)
            {
                Gizmos.DrawSphere(connection.Item1, 0.4f);
                Gizmos.DrawLine(
                    connection.Item1,
                    connection.Item2
                );
            }

            // Nearby doorways that might support connecting corridors.
            Gizmos.color = Color.cyan;

            foreach (var connection in nearbyConnections)
            {
                Gizmos.DrawLine(
                    connection.Item1,
                    connection.Item2
                );
            }
        }
    }

    /// <summary>
    /// Describes a potential replacement for an existing room.
    /// </summary>
    public class UpgradeProposal
    {
        public DungeonRoom OriginalRoom { get; }
        public DungeonRoom ReplacementPrefab { get; }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }

        public Vector3 BoundsCenter { get; }
        public Vector3 BoundsSize { get; }

        public Vector3 NewConnectionPosition { get; }
        public DungeonDoor TargetDoor { get; }

        public UpgradeProposal(
            DungeonRoom originalRoom,
            DungeonRoom replacementPrefab,
            Vector3 position,
            Quaternion rotation,
            Bounds bounds,
            Vector3 newConnectionPosition,
            DungeonDoor targetDoor)
        {
            OriginalRoom = originalRoom;
            ReplacementPrefab = replacementPrefab;

            Position = position;
            Rotation = rotation;

            BoundsCenter = bounds.center;
            BoundsSize = bounds.size;

            NewConnectionPosition = newConnectionPosition;
            TargetDoor = targetDoor;
        }
    }
}