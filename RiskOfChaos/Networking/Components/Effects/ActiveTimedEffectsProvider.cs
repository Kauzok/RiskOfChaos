﻿using HarmonyLib;
using RiskOfChaos.EffectDefinitions;
using RiskOfChaos.EffectHandling;
using RiskOfChaos.EffectHandling.Controllers;
using RiskOfChaos.Networking.SyncLists;
using RiskOfChaos.UI.ActiveEffectsPanel;
using System;
using System.Linq;
using UnityEngine.Networking;

namespace RiskOfChaos.Networking.Components.Effects
{
    public class ActiveTimedEffectsProvider : NetworkBehaviour
    {
        static ActiveTimedEffectsProvider _instance;
        public static ActiveTimedEffectsProvider Instance => _instance;

        static ActiveTimedEffectsProvider()
        {
            RegisterSyncListDelegate(typeof(ActiveTimedEffectsProvider), kListActiveEffects, InvokeSyncListActiveEffects);
            NetworkCRC.RegisterBehaviour(nameof(ActiveTimedEffectsProvider), 0);
        }

        protected static void InvokeSyncListActiveEffects(NetworkBehaviour obj, NetworkReader reader)
        {
            if (!NetworkClient.active)
            {
                Log.Error("Called on server.");
                return;
            }

            ((ActiveTimedEffectsProvider)obj)._activeEffects.HandleMsg(reader);
        }

        public static event Action<ActiveEffectItemInfo[]> OnActiveEffectsChanged;

        const int kListActiveEffects = 43986584;
        readonly SyncListActiveEffectItemInfo _activeEffects = new SyncListActiveEffectItemInfo();

        public int NumActiveDisplayedEffects { get; private set; }

        void Awake()
        {
            _activeEffects.InitializeBehaviour(this, kListActiveEffects);
            _activeEffects.Callback = (op, itemIndex) =>
            {
                onActiveEffectsChanged();
            };
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            onActiveEffectsChanged();
        }

        void OnEnable()
        {
            SingletonHelper.Assign(ref _instance, this);

            TimedChaosEffectHandler.OnTimedEffectStartServer += onTimedEffectStartServer;
            TimedChaosEffectHandler.OnTimedEffectEndServer += onTimedEffectEndServer;
            TimedChaosEffectHandler.OnTimedEffectDirtyServer += refreshEffectDisplay;
            ChaosEffectInfo.OnEffectNameFormatterDirty += ChaosEffectInfo_OnEffectNameFormatterDirty;
        }

        void OnDisable()
        {
            SingletonHelper.Unassign(ref _instance, this);

            TimedChaosEffectHandler.OnTimedEffectStartServer -= onTimedEffectStartServer;
            TimedChaosEffectHandler.OnTimedEffectEndServer -= onTimedEffectEndServer;
            TimedChaosEffectHandler.OnTimedEffectDirtyServer -= refreshEffectDisplay;
            ChaosEffectInfo.OnEffectNameFormatterDirty -= ChaosEffectInfo_OnEffectNameFormatterDirty;
        }

        void ChaosEffectInfo_OnEffectNameFormatterDirty(ChaosEffectInfo effectInfo)
        {
            if (!NetworkServer.active || !TimedChaosEffectHandler.Instance)
                return;

            if (effectInfo is TimedEffectInfo timedEffectInfo)
            {
                TimedChaosEffectHandler.Instance.GetActiveEffects(timedEffectInfo).Do(refreshEffectDisplay);
            }
        }

        void onTimedEffectStartServer(TimedEffect effectInstance)
        {
            _activeEffects.Add(new ActiveEffectItemInfo(effectInstance));
        }

        void onTimedEffectEndServer(TimedEffect effectInstance)
        {
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                if (_activeEffects[i].DispatchID == effectInstance.DispatchID)
                {
                    _activeEffects.RemoveAt(i);
                    return;
                }
            }
        }

        void refreshEffectDisplay(TimedEffect effectInstance)
        {
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                if (_activeEffects[i].DispatchID == effectInstance.DispatchID)
                {
                    _activeEffects[i] = new ActiveEffectItemInfo(effectInstance);
                    return;
                }
            }
        }

        public ActiveEffectItemInfo[] GetActiveEffects()
        {
            return _activeEffects.ToArray();
        }

        void onActiveEffectsChanged()
        {
            NumActiveDisplayedEffects = _activeEffects.Count(e => e.ShouldDisplay);
            OnActiveEffectsChanged?.Invoke(GetActiveEffects());
        }

        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            if (initialState)
            {
                writer.Write(_activeEffects.Count);
                for (ushort i = 0; i < _activeEffects.Count; i++)
                {
                    _activeEffects.SerializeItem(writer, _activeEffects[i]);
                }

                return true;
            }

            return false;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            if (initialState)
            {
                ushort activeEffectsCount = reader.ReadUInt16();

                _activeEffects.Clear();
                for (ushort i = 0; i < activeEffectsCount; i++)
                {
                    _activeEffects.AddInternal(_activeEffects.DeserializeItem(reader));
                }

                return;
            }
        }
    }
}
