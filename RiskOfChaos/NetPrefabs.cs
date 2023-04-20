﻿using R2API;
using RiskOfChaos.Components;
using RiskOfChaos.GravityModifier;
using RiskOfChaos.Networking.Components;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace RiskOfChaos
{
    public static class NetPrefabs
    {
        public static GameObject GenericTeamInventoryPrefab { get; private set; }

        public static GameObject GravityControllerPrefab { get; private set; }

        static GameObject createPrefabObject(string name, bool networked = true)
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
                GenericTeamInventoryPrefab = createPrefabObject("GenericTeamInventory");

                GenericTeamInventoryPrefab.AddComponent<SetDontDestroyOnLoad>();
                GenericTeamInventoryPrefab.AddComponent<TeamFilter>();
                GenericTeamInventoryPrefab.AddComponent<Inventory>();
                GenericTeamInventoryPrefab.AddComponent<EnemyInfoPanelInventoryProvider>();
                GenericTeamInventoryPrefab.AddComponent<DestroyOnRunEnd>();
            }

            // NetworkGravityControllerPrefab
            {
                GravityControllerPrefab = createPrefabObject("GravityController");

                GravityControllerPrefab.AddComponent<SetDontDestroyOnLoad>();
                GravityControllerPrefab.AddComponent<DestroyOnRunEnd>();
                GravityControllerPrefab.AddComponent<SyncWorldGravity>();
                GravityControllerPrefab.AddComponent<GravityModificationManager>();
            }

            Run.onRunStartGlobal += onRunStart;
        }

        static void onRunStart(Run _)
        {
            if (!NetworkServer.active)
                return;

            GameObject networkGravityController = GameObject.Instantiate(GravityControllerPrefab);
            NetworkServer.Spawn(networkGravityController);
        }
    }
}
