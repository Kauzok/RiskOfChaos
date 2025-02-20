﻿using RiskOfChaos.Utilities.Extensions;
using RoR2;
using RoR2.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace RiskOfChaos.Utilities
{
    public static class SpawnUtils
    {
        public static class ExtraPlacementModes
        {
            public const DirectorPlacementRule.PlacementMode NearestNodeWithConditions = (DirectorPlacementRule.PlacementMode)134;

            [SystemInitializer]
            static void Init()
            {
                On.RoR2.DirectorCore.TrySpawnObject += (orig, self, directorSpawnRequest) =>
                {
                    GameObject result = orig(self, directorSpawnRequest);
                    if (result)
                        return result;

                    SpawnCard spawnCard = directorSpawnRequest.spawnCard;
                    if (!spawnCard)
                        return null;

                    NodeGraph nodeGraph = SceneInfo.instance.GetNodeGraph(spawnCard.nodeGraphType);
                    if (!nodeGraph)
                        return null;

                    DirectorPlacementRule placementRule = directorSpawnRequest.placementRule;

                    Quaternion getRotationFacingTargetPositionFromPoint(Vector3 point)
                    {
                        Vector3 targetPosition = placementRule.targetPosition;
                        point.y = targetPosition.y;
                        return Util.QuaternionSafeLookRotation(placementRule.targetPosition - point);
                    }

                    GameObject spawnAt(Vector3 position)
                    {
                        return spawnCard.DoSpawn(position, getRotationFacingTargetPositionFromPoint(position), directorSpawnRequest).spawnedInstance;
                    }

                    switch (placementRule.placementMode)
                    {
                        case NearestNodeWithConditions:
                            List<NodeGraph.NodeIndex> validNodes = nodeGraph.FindNodesInRangeWithFlagConditions(placementRule.position, placementRule.minDistance, placementRule.maxDistance, (HullMask)(1 << (int)spawnCard.hullSize), spawnCard.requiredFlags, spawnCard.forbiddenFlags, placementRule.preventOverhead);
                            int nodesRemoved = validNodes.RemoveAll(n => !nodeGraph.GetNodePosition(n, out _));
#if DEBUG
                            if (nodesRemoved > 0)
                            {
                                Log.Debug($"Excluded {nodesRemoved} invalid nodes");
                            }
#endif

                            NodeGraph.NodeIndex? closestValidNodeIndex = null;
                            float closestNodeSqrDistance = float.PositiveInfinity;

                            foreach (NodeGraph.NodeIndex nodeIndex in validNodes)
                            {
                                if (nodeGraph.GetNodePosition(nodeIndex, out Vector3 position))
                                {
                                    float sqrDistance = (placementRule.position - position).sqrMagnitude;

                                    if (!closestValidNodeIndex.HasValue || sqrDistance < closestNodeSqrDistance)
                                    {
#pragma warning disable Publicizer001 // Accessing a member that was not originally public
                                        bool positionFree = self.CheckPositionFree(nodeGraph, nodeIndex, spawnCard);
#pragma warning restore Publicizer001 // Accessing a member that was not originally public

                                        if (positionFree)
                                        {
                                            closestValidNodeIndex = nodeIndex;
                                            closestNodeSqrDistance = sqrDistance;
                                        }
                                    }
                                }
                            }

                            if (closestValidNodeIndex.HasValue)
                            {
                                NodeGraph.NodeIndex nodeIndex = closestValidNodeIndex.Value;

                                if (spawnCard.occupyPosition)
                                    self.AddOccupiedNode(nodeGraph, nodeIndex);

                                nodeGraph.GetNodePosition(nodeIndex, out Vector3 position);
                                return spawnAt(position);
                            }

                            Log.Info($"ExtraPlacementModes.NearestNodeWithConditions: Could not find nodes satisfying conditions for {spawnCard.name}. targetPosition={placementRule.targetPosition}, minDistance={placementRule.minDistance}, maxDistance={placementRule.maxDistance}, hullSize={spawnCard.hullSize}, requiredFlags={spawnCard.requiredFlags}, forbiddenFlags={spawnCard.forbiddenFlags}, preventOverhead={placementRule.preventOverhead}");
                            break;
                    }

                    return null;
                };
            }
        }

        static readonly SpawnCard _positionHelperSpawnCard;

        static SpawnUtils()
        {
            _positionHelperSpawnCard = ScriptableObject.CreateInstance<SpawnCard>();

            const string HELPER_PREFAB_PATH = "SpawnCards/HelperPrefab";
            _positionHelperSpawnCard.prefab = LegacyResourcesAPI.Load<GameObject>(HELPER_PREFAB_PATH);
            _positionHelperSpawnCard.sendOverNetwork = false;
            _positionHelperSpawnCard.hullSize = HullClassification.Human;
            _positionHelperSpawnCard.nodeGraphType = MapNodeGroup.GraphType.Ground;

            if (!_positionHelperSpawnCard.prefab)
            {
                Log.Error($"{HELPER_PREFAB_PATH} is null");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DirectorPlacementRule.PlacementMode GetBestValidRandomPlacementType()
        {
            SceneInfo sceneInfo = SceneInfo.instance;
            return sceneInfo && sceneInfo.approximateMapBoundMesh != null ? DirectorPlacementRule.PlacementMode.RandomNormalized : DirectorPlacementRule.PlacementMode.Random;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DirectorPlacementRule GetBestValidRandomPlacementRule()
        {
            return new DirectorPlacementRule
            {
                placementMode = GetBestValidRandomPlacementType()
            };
        }

        public static Vector3 EvaluateToPosition(this DirectorPlacementRule placementRule, Xoroshiro128Plus rng, HullClassification? overrideHullSize = null, MapNodeGroup.GraphType? overrideNodeGraphType = null, NodeFlags? requiredFlags = null, NodeFlags? forbiddenFlags = null)
        {
            if (!_positionHelperSpawnCard.prefab)
            {
                Log.Error("Null position helper prefab");
                return Vector3.zero;
            }

            DirectorCore directorCore = DirectorCore.instance;
            if (!directorCore)
            {
                Log.Error($"No {nameof(DirectorCore)} instance found");
                return Vector3.zero;
            }

            HullClassification originalHullSize = _positionHelperSpawnCard.hullSize;
            MapNodeGroup.GraphType originalNodeGraphType = _positionHelperSpawnCard.nodeGraphType;
            NodeFlags originalRequiredFlags = _positionHelperSpawnCard.requiredFlags;
            NodeFlags originalForbiddenFlags = _positionHelperSpawnCard.forbiddenFlags;

            if (overrideHullSize.HasValue)
                _positionHelperSpawnCard.hullSize = overrideHullSize.Value;

            if (overrideNodeGraphType.HasValue)
                _positionHelperSpawnCard.nodeGraphType = overrideNodeGraphType.Value;

            if (requiredFlags.HasValue)
                _positionHelperSpawnCard.requiredFlags = requiredFlags.Value;

            if (forbiddenFlags.HasValue)
                _positionHelperSpawnCard.forbiddenFlags = forbiddenFlags.Value;

            try
            {
                GameObject positionMarkerObject = directorCore.TrySpawnObject(new DirectorSpawnRequest(_positionHelperSpawnCard, placementRule, rng));

                if (!positionMarkerObject)
                {
                    Log.Warning("Unable to spawn position marker object");
                    return placementRule.targetPosition;
                }

                Vector3 position = positionMarkerObject.transform.position;

                GameObject.Destroy(positionMarkerObject);

                return position;
            }
            finally
            {
                _positionHelperSpawnCard.hullSize = originalHullSize;
                _positionHelperSpawnCard.nodeGraphType = originalNodeGraphType;
                _positionHelperSpawnCard.requiredFlags = originalRequiredFlags;
                _positionHelperSpawnCard.forbiddenFlags = originalForbiddenFlags;
            }
        }

        public static DirectorPlacementRule GetPlacementRule_AtRandomPlayerDirect(Xoroshiro128Plus rng)
        {
            CharacterBody[] playerBodies = PlayerUtils.GetAllPlayerBodies(true).ToArray();
            if (playerBodies.Length > 0)
            {
                CharacterBody selectedPlayer = rng.NextElementUniform(playerBodies);

                return new DirectorPlacementRule
                {
                    placementMode = DirectorPlacementRule.PlacementMode.Direct,
                    position = selectedPlayer.footPosition
                };
            }
            else
            {
                return GetBestValidRandomPlacementRule();
            }
        }

        public static DirectorPlacementRule GetPlacementRule_AtRandomPlayerNearestNode(Xoroshiro128Plus rng)
        {
            CharacterBody[] playerBodies = PlayerUtils.GetAllPlayerBodies(true).ToArray();
            if (playerBodies.Length > 0)
            {
                CharacterBody selectedPlayer = rng.NextElementUniform(playerBodies);

                return new DirectorPlacementRule
                {
                    placementMode = DirectorPlacementRule.PlacementMode.NearestNode,
                    position = selectedPlayer.footPosition
                };
            }
            else
            {
                return GetBestValidRandomPlacementRule();
            }
        }

        public static DirectorPlacementRule GetPlacementRule_AtRandomPlayerApproximate(Xoroshiro128Plus rng, float minPlayerDistance, float maxPlayerDistance)
        {
            CharacterBody[] playerBodies = PlayerUtils.GetAllPlayerBodies(true).ToArray();
            if (playerBodies.Length > 0)
            {
                CharacterBody selectedPlayer = rng.NextElementUniform(playerBodies);

                return new DirectorPlacementRule
                {
                    placementMode = DirectorPlacementRule.PlacementMode.Approximate,
                    position = selectedPlayer.footPosition,
                    minDistance = minPlayerDistance,
                    maxDistance = maxPlayerDistance
                };
            }
            else
            {
                return GetBestValidRandomPlacementRule();
            }
        }

        public static DirectorPlacementRule GetPlacementRule_AtRandomPlayerNearestNode(Xoroshiro128Plus rng, float minPlayerDistance, float maxPlayerDistance)
        {
            CharacterBody[] playerBodies = PlayerUtils.GetAllPlayerBodies(true).ToArray();
            if (playerBodies.Length > 0)
            {
                CharacterBody selectedPlayer = rng.NextElementUniform(playerBodies);

                return new DirectorPlacementRule
                {
                    placementMode = ExtraPlacementModes.NearestNodeWithConditions,
                    position = selectedPlayer.footPosition,
                    minDistance = minPlayerDistance,
                    maxDistance = maxPlayerDistance
                };
            }
            else
            {
                return GetBestValidRandomPlacementRule();
            }
        }

        public static bool HasValidSpawnLocation(this SpawnCard card)
        {
            if (!card || !card.prefab)
            {
#if DEBUG
                Log.Debug($"Unable to spawn null card {card}");
#endif
                return false;
            }

            SceneInfo sceneInfo = SceneInfo.instance;
            if (!sceneInfo)
            {
#if DEBUG
                Log.Debug($"Unable to spawn {card}: No SceneInfo instance");
#endif
                return false;
            }

            NodeGraph cardNodeGraph = sceneInfo.GetNodeGraph(card.nodeGraphType);
            if (!cardNodeGraph)
            {
#if DEBUG
                Log.Debug($"Unable to spawn {card}: Target node graph {card.nodeGraphType} does not exist");
#endif
                return false;
            }

            List<NodeGraph.NodeIndex> validNodes = cardNodeGraph.GetActiveNodesForHullMaskWithFlagConditions((HullMask)(1 << (int)card.hullSize), card.requiredFlags, card.forbiddenFlags);
            if (validNodes.Count == 0)
            {
#if DEBUG
                Log.Debug($"Unable to spawn {card}: No active nodes matches flags");
#endif
                return false;
            }

            return true;
        }

        public static Vector3 GetEnvironmentNormalAtPoint(Vector3 position, float backtrackDistance = 1f)
        {
            return GetEnvironmentNormalAtPoint(position, Vector3.up, backtrackDistance);
        }

        public static Vector3 GetEnvironmentNormalAtPoint(Vector3 position, Vector3 up, float backtrackDistance = 1f)
        {
            if (Physics.Raycast(new Ray(position + (up * backtrackDistance), -up), out RaycastHit hit, backtrackDistance * 1.5f, LayerIndex.world.mask))
            {
                return hit.normal;
            }

            return Vector3.up;
        }

        public static GameObject SpawnWithFallbackPlacement(this DirectorSpawnRequest spawnRequest, DirectorPlacementRule fallbackPlacementRule)
        {
            GameObject result = DirectorCore.instance.TrySpawnObject(spawnRequest);
            if (!result)
            {
                DirectorPlacementRule previousPlacementRule = spawnRequest.placementRule;

                spawnRequest.placementRule = fallbackPlacementRule;
                result = DirectorCore.instance.TrySpawnObject(spawnRequest);

                spawnRequest.placementRule = previousPlacementRule;
            }

            return result;
        }

        [Flags]
        public enum NodeGraphFlags : byte
        {
            None = 0,
            Ground = 1 << 0,
            Air = 1 << 1,
            Rail = 1 << 2,
            All = byte.MaxValue
        }

        public record struct NodeReference(NodeGraph NodeGraph, NodeGraph.NodeIndex NodeIndex)
        {
            public readonly bool TryGetPosition(out Vector3 position)
            {
                return NodeGraph.GetNodePosition(NodeIndex, out position);
            }
        }

        public readonly record struct NodeSelectionRules(NodeGraphFlags GraphMask, bool RequireFree, HullMask HullMask, NodeFlags RequiredFlags, NodeFlags ForbiddenFlags);

        public static IEnumerable<NodeReference> GetNodes(NodeSelectionRules nodeSelectionRules)
        {
            SceneInfo sceneInfo = SceneInfo.instance;
            if (!sceneInfo)
            {
                Log.Error("No SceneInfo");
                yield break;
            }

            IEnumerable<NodeGraph.NodeIndex> getValidNodes(NodeGraph nodeGraph)
            {
                return nodeGraph.GetActiveNodesForHullMaskWithFlagConditions(nodeSelectionRules.HullMask, nodeSelectionRules.RequiredFlags, nodeSelectionRules.ForbiddenFlags);
            }

            if (sceneInfo.groundNodes && (nodeSelectionRules.GraphMask & NodeGraphFlags.Ground) != 0)
            {
                foreach (NodeGraph.NodeIndex nodeIndex in getValidNodes(sceneInfo.groundNodes))
                {
                    yield return new NodeReference(sceneInfo.groundNodes, nodeIndex);
                }
            }

            if (sceneInfo.airNodes && (nodeSelectionRules.GraphMask & NodeGraphFlags.Air) != 0)
            {
                foreach (NodeGraph.NodeIndex nodeIndex in getValidNodes(sceneInfo.airNodes))
                {
                    yield return new NodeReference(sceneInfo.airNodes, nodeIndex);
                }
            }

            if (sceneInfo.railNodes && (nodeSelectionRules.GraphMask & NodeGraphFlags.Rail) != 0)
            {
                foreach (NodeGraph.NodeIndex nodeIndex in getValidNodes(sceneInfo.railNodes))
                {
                    yield return new NodeReference(sceneInfo.railNodes, nodeIndex);
                }
            }
        }

        public static IEnumerable<Vector3> GenerateDistributedSpawnPositions(NodeSelectionRules selectionRules, float selectionFraction, Xoroshiro128Plus rng)
        {
            SceneInfo sceneInfo = SceneInfo.instance;
            if (!sceneInfo)
            {
                Log.Error("No positions could be generated, no sceneInfo");
                yield break;
            }

            NodeReference[] nodes = GetNodes(selectionRules).ToArray();

            if (nodes.Length <= 0)
            {
                Log.Error("No valid nodes matches flags");
                yield break;
            }

            Util.ShuffleArray(nodes, rng.Branch());

            int targetNodesCount = Mathf.Max(1, Mathf.FloorToInt(nodes.Length * selectionFraction));

            int nodeIndex;
            for (nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
            {
                if (nodeIndex >= targetNodesCount)
                    break;

                NodeReference node = nodes[nodeIndex];

                if (selectionRules.RequireFree)
                {
                    DirectorCore directorCore = DirectorCore.instance;
                    if (directorCore)
                    {
#pragma warning disable Publicizer001 // Accessing a member that was not originally public
                        DirectorCore.NodeReference[] occupiedNodes = directorCore.occupiedNodes;
#pragma warning restore Publicizer001 // Accessing a member that was not originally public
                        if (Array.IndexOf(occupiedNodes, new DirectorCore.NodeReference(node.NodeGraph, node.NodeIndex)) >= 0)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        Log.Warning("No DirectorCore instance");
                    }
                }

                if (node.TryGetPosition(out Vector3 position))
                {
                    yield return position;
                }
            }

            if (targetNodesCount > nodeIndex)
            {
                Log.Warning($"Not enough nodes were valid, skipping {targetNodesCount - nodeIndex} of the requested nodes");
            }
            else
            {
#if DEBUG
                Log.Debug($"Generated {targetNodesCount} positions");
#endif
            }
        }
    }
}
