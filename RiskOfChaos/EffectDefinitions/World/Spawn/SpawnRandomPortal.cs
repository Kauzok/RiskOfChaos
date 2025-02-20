﻿using RiskOfChaos.EffectHandling.EffectClassAttributes;
using RiskOfChaos.EffectHandling.EffectClassAttributes.Methods;
using RiskOfChaos.Utilities;
using RiskOfChaos.Utilities.Extensions;
using RoR2;

namespace RiskOfChaos.EffectDefinitions.World.Spawn
{
    [ChaosEffect("spawn_random_portal")]
    public sealed class SpawnRandomPortal : GenericDirectorSpawnEffect<InteractableSpawnCard>
    {
        static SpawnCardEntry[] _spawnCards;

        [SystemInitializer]
        static void Init()
        {
            _spawnCards = new SpawnCardEntry[]
            {
                loadBasicSpawnEntry("RoR2/Base/PortalGoldshores/iscGoldshoresPortal.asset", 1.2f),
                loadBasicSpawnEntry("RoR2/Base/PortalMS/iscMSPortal.asset", 1.2f),
                loadBasicSpawnEntry("RoR2/Base/PortalShop/iscShopPortal.asset", 1.2f),
                loadBasicSpawnEntry("RoR2/DLC1/GameModes/InfiniteTowerRun/InfiniteTowerAssets/iscInfiniteTowerPortal.asset"),
                loadBasicSpawnEntry("RoR2/DLC1/DeepVoidPortal/iscDeepVoidPortal.asset", 0.8f),
                loadBasicSpawnEntry("RoR2/DLC1/PortalVoid/iscVoidPortal.asset", 0.8f),
                loadBasicSpawnEntry("RoR2/DLC1/VoidOutroPortal/iscVoidOutroPortal.asset", 0.8f)
            };
        }

        [EffectCanActivate]
        static bool CanActivate()
        {
            return areAnyAvailable(_spawnCards);
        }

        public override void OnStart()
        {
            DirectorPlacementRule placementRule = SpawnUtils.GetPlacementRule_AtRandomPlayerNearestNode(RNG.Branch(), 0f, float.PositiveInfinity);
            DirectorSpawnRequest spawnRequest = new DirectorSpawnRequest(getItemToSpawn(_spawnCards, RNG), placementRule, RNG.Branch());

            spawnRequest.SpawnWithFallbackPlacement(SpawnUtils.GetBestValidRandomPlacementRule());
        }
    }
}
