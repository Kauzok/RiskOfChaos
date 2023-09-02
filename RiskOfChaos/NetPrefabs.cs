﻿using R2API;
using RiskOfChaos.Components;
using RiskOfChaos.ModifierController.AttackDelay;
using RiskOfChaos.ModifierController.Damage;
using RiskOfChaos.ModifierController.Gravity;
using RiskOfChaos.ModifierController.Knockback;
using RiskOfChaos.ModifierController.PhysicsModification;
using RiskOfChaos.ModifierController.Projectile;
using RiskOfChaos.ModifierController.SkillSlots;
using RiskOfChaos.ModifierController.TimeScale;
using RiskOfChaos.Networking.Components;
using RiskOfChaos.Networking.Components.Gravity;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace RiskOfChaos
{
    public static class NetPrefabs
    {
        public static GameObject GenericTeamInventoryPrefab { get; private set; }

        public static GameObject MonsterItemStealControllerPrefab { get; private set; }

        public static GameObject GravityControllerPrefab { get; private set; }

        public static GameObject SkillSlotModificationControllerPrefab { get; private set; }

        public static GameObject KnockbackModificationControllerPrefab { get; private set; }

        public static GameObject ProjectileModificationControllerPrefab { get; private set; }

        public static GameObject TimeScaleModificationControllerPrefab { get; private set; }

        public static GameObject DamageInfoModificationControllerPrefab { get; private set; }

        public static GameObject PhysicsModificationControllerPrefab { get; private set; }

        public static GameObject AttackDelayModificationControllerPrefab { get; private set; }

        static readonly string[] _geyserPrefabPaths = new string[]
        {
            "RoR2/Base/Common/Props/Geyser.prefab",
            "RoR2/Base/artifactworld/AWGeyser.prefab",
            "RoR2/Base/moon/MoonGeyser.prefab",
            "RoR2/DLC1/ancientloft/AncientLoft_Geyser.prefab",
            "RoR2/DLC1/snowyforest/SFGeyser.prefab"
        };
        public static GameObject[] GeyserPrefabs { get; private set; }

        public static GameObject ActiveTimedEffectsProviderPrefab { get; private set; }

        public static GameObject ItemStealerPositionIndicatorPrefab { get; private set; }

        public static GameObject SulfurPodBasePrefab { get; private set; }

        static GameObject createEmptyPrefabObject(string name, bool networked = true)
        {
            GameObject tmp = new GameObject(name);

            if (networked)
            {
                tmp.AddComponent<NetworkIdentity>();
            }

            GameObject prefab = tmp.InstantiateClone(Main.PluginGUID + "_" + name, networked);
            GameObject.Destroy(tmp);

            return prefab;
        }

        internal static void InitializeAll()
        {
            // GenericTeamInventoryPrefab
            {
                GenericTeamInventoryPrefab = createEmptyPrefabObject("GenericTeamInventory");

                GenericTeamInventoryPrefab.AddComponent<SetDontDestroyOnLoad>();
                GenericTeamInventoryPrefab.AddComponent<TeamFilter>();
                GenericTeamInventoryPrefab.AddComponent<Inventory>();
                GenericTeamInventoryPrefab.AddComponent<EnemyInfoPanelInventoryProvider>();
                GenericTeamInventoryPrefab.AddComponent<DestroyOnRunEnd>();
            }

            // MonsterItemStealControllerPrefab
            {
                GameObject itemStealControllerPrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Brother/ItemStealController.prefab").WaitForCompletion();

                MonsterItemStealControllerPrefab = itemStealControllerPrefab.InstantiateClone(Main.PluginGUID + "_" + "MonsterItemStealController", true);

                NetworkedBodyAttachment networkedBodyAttachment = MonsterItemStealControllerPrefab.GetComponent<NetworkedBodyAttachment>();
                networkedBodyAttachment.shouldParentToAttachedBody = true;

                ItemStealController itemStealController = MonsterItemStealControllerPrefab.GetComponent<ItemStealController>();
                itemStealController.stealInterval = 0.2f;

                MonsterItemStealControllerPrefab.AddComponent<SyncStolenItemCount>();
                MonsterItemStealControllerPrefab.AddComponent<ShowStolenItemsPositionIndicator>();
            }

            // NetworkGravityControllerPrefab
            {
                GravityControllerPrefab = createEmptyPrefabObject("GravityController");

                GravityControllerPrefab.AddComponent<SetDontDestroyOnLoad>();
                GravityControllerPrefab.AddComponent<DestroyOnRunEnd>();
                GravityControllerPrefab.AddComponent<SyncWorldGravity>();
                GravityControllerPrefab.AddComponent<SyncBaseGravity>();
                GravityControllerPrefab.AddComponent<GravityModificationManager>();
            }

            // SkillSlotModificationControllerPrefab
            {
                SkillSlotModificationControllerPrefab = createEmptyPrefabObject("SkillSlotModificationController");

                SkillSlotModificationControllerPrefab.AddComponent<SetDontDestroyOnLoad>();
                SkillSlotModificationControllerPrefab.AddComponent<DestroyOnRunEnd>();
                SkillSlotModificationControllerPrefab.AddComponent<SkillSlotModificationManager>();
            }

            // KnockbackModificationControllerPrefab
            {
                KnockbackModificationControllerPrefab = createEmptyPrefabObject("KnockbackModificationController");

                KnockbackModificationControllerPrefab.AddComponent<SetDontDestroyOnLoad>();
                KnockbackModificationControllerPrefab.AddComponent<DestroyOnRunEnd>();
                KnockbackModificationControllerPrefab.AddComponent<KnockbackModificationManager>();
            }

            // ProjectileModificationControllerPrefab
            {
                ProjectileModificationControllerPrefab = createEmptyPrefabObject("ProjectileModificationController");

                ProjectileModificationControllerPrefab.AddComponent<SetDontDestroyOnLoad>();
                ProjectileModificationControllerPrefab.AddComponent<DestroyOnRunEnd>();
                ProjectileModificationControllerPrefab.AddComponent<ProjectileModificationManager>();
            }

            // TimeScaleModificationControllerPrefab
            {
                TimeScaleModificationControllerPrefab = createEmptyPrefabObject("TimeScaleModificationController");

                TimeScaleModificationControllerPrefab.AddComponent<SetDontDestroyOnLoad>();
                TimeScaleModificationControllerPrefab.AddComponent<DestroyOnRunEnd>();
                TimeScaleModificationControllerPrefab.AddComponent<SyncTimeScale>();
                TimeScaleModificationControllerPrefab.AddComponent<TimeScaleModificationManager>();
            }

            // DamageInfoModificationControllerPrefab
            {
                DamageInfoModificationControllerPrefab = createEmptyPrefabObject("DamageInfoModificationController", false);

                DamageInfoModificationControllerPrefab.AddComponent<SetDontDestroyOnLoad>();
                DamageInfoModificationControllerPrefab.AddComponent<DestroyOnRunEnd>();
                DamageInfoModificationControllerPrefab.AddComponent<DamageInfoModificationManager>();
            }

            // PhysicsModificationControllerPrefab
            {
                PhysicsModificationControllerPrefab = createEmptyPrefabObject("PhysicsModificationController");

                PhysicsModificationControllerPrefab.AddComponent<SetDontDestroyOnLoad>();
                PhysicsModificationControllerPrefab.AddComponent<DestroyOnRunEnd>();
                PhysicsModificationControllerPrefab.AddComponent<PhysicsModificationManager>();
                PhysicsModificationControllerPrefab.AddComponent<ModifiedPhysicsSimulator>();
            }

            // AttackDelayModificationControllerPrefab
            {
                AttackDelayModificationControllerPrefab = createEmptyPrefabObject("AttackDelayModificationController");

                AttackDelayModificationControllerPrefab.AddComponent<SetDontDestroyOnLoad>();
                AttackDelayModificationControllerPrefab.AddComponent<DestroyOnRunEnd>();
                AttackDelayModificationControllerPrefab.AddComponent<AttackDelayModificationManager>();
            }

            // GeyserPrefabs
            {
                int geyserCount = _geyserPrefabPaths.Length;
                GeyserPrefabs = new GameObject[geyserCount];
                for (int i = 0; i < geyserCount; i++)
                {
                    GameObject geyserPrefab = Addressables.LoadAssetAsync<GameObject>(_geyserPrefabPaths[i]).WaitForCompletion();
                    string prefabName = geyserPrefab.name;

                    GameObject tmpNetworkGeyserPrefab = GameObject.Instantiate(geyserPrefab);

                    tmpNetworkGeyserPrefab.AddComponent<NetworkIdentity>();
                    tmpNetworkGeyserPrefab.AddComponent<SyncJumpVolumeVelocity>();
                    GeyserPrefabs[i] = tmpNetworkGeyserPrefab.InstantiateClone(Main.PluginGUID + "_Networked" + prefabName, true);

                    GameObject.Destroy(tmpNetworkGeyserPrefab);
                }
            }

            // ActiveTimedEffectsProviderPrefab
            {
                ActiveTimedEffectsProviderPrefab = createEmptyPrefabObject("ActiveTimedEffectsProvider");

                ActiveTimedEffectsProviderPrefab.AddComponent<SetDontDestroyOnLoad>();
                ActiveTimedEffectsProviderPrefab.AddComponent<DestroyOnRunEnd>();
                ActiveTimedEffectsProviderPrefab.AddComponent<ActiveTimedEffectsProvider>();
            }

            // ItemStealerPositionIndicatorPrefab
            {
                ItemStealerPositionIndicatorPrefab = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Common/BossPositionIndicator.prefab").WaitForCompletion().InstantiateClone("ItemStealerPositionIndicator", false);

                PositionIndicator positionIndicator = ItemStealerPositionIndicatorPrefab.GetComponent<PositionIndicator>();

                if (positionIndicator.insideViewObject)
                {
                    foreach (SpriteRenderer insideSprite in positionIndicator.insideViewObject.GetComponentsInChildren<SpriteRenderer>())
                    {
                        insideSprite.color = Color.cyan;
                    }
                }

                if (positionIndicator.outsideViewObject)
                {
                    foreach (SpriteRenderer outsideSprite in positionIndicator.outsideViewObject.GetComponentsInChildren<SpriteRenderer>())
                    {
                        outsideSprite.color = Color.cyan;
                    }
                }
            }

            // SulfurPodBasePrefab
            {
                GameObject prefab = Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/sulfurpools/SPSulfurPodBase.prefab").WaitForCompletion();
                string prefabName = prefab.name;

                GameObject tmp = GameObject.Instantiate(prefab);
                tmp.AddComponent<NetworkIdentity>();

                SulfurPodBasePrefab = tmp.InstantiateClone(Main.PluginGUID + "_Networked" + prefabName, true);

                GameObject.Destroy(tmp);
            }

            Run.onRunStartGlobal += onRunStart;
        }

        static void onRunStart(Run _)
        {
            if (!NetworkServer.active)
                return;

            NetworkServer.Spawn(GameObject.Instantiate(GravityControllerPrefab));
            NetworkServer.Spawn(GameObject.Instantiate(SkillSlotModificationControllerPrefab));
            NetworkServer.Spawn(GameObject.Instantiate(KnockbackModificationControllerPrefab));
            NetworkServer.Spawn(GameObject.Instantiate(ProjectileModificationControllerPrefab));
            NetworkServer.Spawn(GameObject.Instantiate(TimeScaleModificationControllerPrefab));
            NetworkServer.Spawn(GameObject.Instantiate(PhysicsModificationControllerPrefab));
            NetworkServer.Spawn(GameObject.Instantiate(AttackDelayModificationControllerPrefab));

            GameObject.Instantiate(DamageInfoModificationControllerPrefab);

            NetworkServer.Spawn(GameObject.Instantiate(ActiveTimedEffectsProviderPrefab));
        }
    }
}
