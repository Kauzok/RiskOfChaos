﻿using RiskOfChaos.EffectHandling;
using RoR2;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using UnityEngine;

namespace RiskOfChaos.EffectDefinitions
{
    [ChaosEffect("DuplicateAllCharacters", EffectRepetitionWeightExponent = 75f)]
    public class DuplicateAllCharacters : BaseEffect
    {
        public override void OnStart()
        {
            ReadOnlyCollection<CharacterBody> allCharacterBodies = CharacterBody.readOnlyInstancesList;
            for (int i = allCharacterBodies.Count - 1; i >= 0; i--)
            {
                CharacterBody body = allCharacterBodies[i];
                if (!body)
                    continue;

                if ((body.bodyFlags & CharacterBody.BodyFlags.Masterless) != 0)
                    continue;

                CharacterMaster master = body.master;
                if (!master)
                    continue;
                
                duplicateMaster(master);
            }
        }

        void duplicateMaster(CharacterMaster master)
        {
            CharacterBody body = master.GetBody();
            if (!body)
                return;

            MasterCopySpawnCard copySpawnCard = MasterCopySpawnCard.FromMaster(master, true, true);

            DirectorPlacementRule placement = new DirectorPlacementRule
            {
                placementMode = DirectorPlacementRule.PlacementMode.NearestNode,
                position = body.footPosition,
                minDistance = 0f,
                maxDistance = float.PositiveInfinity
            };

            DirectorSpawnRequest spawnRequest = new DirectorSpawnRequest(copySpawnCard, placement, new Xoroshiro128Plus(RNG.nextUlong))
            {
                summonerBodyObject = body.gameObject,
                ignoreTeamMemberLimit = true
            };

            DirectorCore.instance.TrySpawnObject(spawnRequest);
            GameObject.Destroy(copySpawnCard);
        }
    }
}
