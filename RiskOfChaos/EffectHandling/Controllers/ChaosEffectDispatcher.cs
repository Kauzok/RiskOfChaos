﻿using R2API.Networking;
using R2API.Networking.Interfaces;
using RiskOfChaos.EffectDefinitions;
using RiskOfChaos.Networking;
using RiskOfChaos.SaveHandling;
using RiskOfChaos.SaveHandling.DataContainers;
using RiskOfChaos.SaveHandling.DataContainers.EffectHandlerControllers;
using RoR2;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace RiskOfChaos.EffectHandling.Controllers
{
    [ChaosController(false)]
    public class ChaosEffectDispatcher : MonoBehaviour
    {
        static ChaosEffectDispatcher _instance;
        public static ChaosEffectDispatcher Instance => _instance;

        public delegate void EffectDispatchedDelegate(ChaosEffectInfo effectInfo, in ChaosEffectDispatchArgs args, BaseEffect effectInstance);
        public event EffectDispatchedDelegate OnEffectDispatched;

        public delegate void EffectAboutToDispatchDelegate(ChaosEffectInfo effectInfo, in ChaosEffectDispatchArgs args, bool willStart);
        public event EffectAboutToDispatchDelegate OnEffectAboutToDispatchServer;

        ChaosEffectActivationSignaler[] _effectActivationSignalers;

        Xoroshiro128Plus _effectRNG;
        ulong _effectDispatchCount;

        public bool HasAttemptedDispatchAnyEffectServer { get; private set; }

        void Awake()
        {
            _effectActivationSignalers = GetComponents<ChaosEffectActivationSignaler>();
        }

        void OnEnable()
        {
            SingletonHelper.Assign(ref _instance, this);

            if (NetworkServer.active)
            {
                foreach (ChaosEffectActivationSignaler activationSignaler in _effectActivationSignalers)
                {
                    activationSignaler.SignalShouldDispatchEffect += ActivationSignaler_SignalShouldDispatchEffect;
                }

                if (Run.instance)
                {
                    _effectRNG = new Xoroshiro128Plus(Run.instance.seed);
                }

                _effectDispatchCount = 0;

                if (SaveManager.UseSaveData)
                {
                    SaveManager.CollectSaveData += SaveManager_CollectSaveData;
                    SaveManager.LoadSaveData += SaveManager_LoadSaveData;
                }
            }
            else
            {
                NetworkedEffectDispatchedMessage.OnReceive += NetworkedEffectDispatchedMessage_OnReceive;
            }

            HasAttemptedDispatchAnyEffectServer = false;
        }

        void Update()
        {
            if (!NetworkServer.active)
                return;

            foreach (ChaosEffectInfo effectInfo in ChaosEffectCatalog.AllEffects)
            {
                if (effectInfo.IsActivationShortcutPressed)
                {
                    if (effectInfo.CanActivate(EffectCanActivateContext.Now))
                    {
                        dispatchEffect(effectInfo);
                    }
                    else
                    {
                        ChaosEffectActivationSoundHandler.PlayEffectActivatedSound();

                        Chat.SendBroadcastChat(new Chat.SimpleChatMessage
                        {
                            baseToken = "CHAOS_EFFECT_SHORTCUT_CANNOT_ACTIVATE",
                            paramTokens = new string[] { effectInfo.GetDisplayName(EffectNameFormatFlags.RuntimeFormatArgs) }
                        });
                    }
                }
            }
        }

        void OnDisable()
        {
            SingletonHelper.Unassign(ref _instance, this);

            foreach (ChaosEffectActivationSignaler activationSignaler in _effectActivationSignalers)
            {
                activationSignaler.SignalShouldDispatchEffect -= ActivationSignaler_SignalShouldDispatchEffect;
            }

            _effectRNG = null;

            NetworkedEffectDispatchedMessage.OnReceive -= NetworkedEffectDispatchedMessage_OnReceive;

            SaveManager.CollectSaveData -= SaveManager_CollectSaveData;
            SaveManager.LoadSaveData -= SaveManager_LoadSaveData;
        }

        void SaveManager_CollectSaveData(ref SaveContainer container)
        {
            container.DispatcherData = new EffectDispatcherData
            {
                EffectRNG = new SerializableRng(_effectRNG),
                EffectDispatchCount = _effectDispatchCount
            };
        }

        void SaveManager_LoadSaveData(in SaveContainer container)
        {
            EffectDispatcherData data = container.DispatcherData;
            if (data is null)
                return;

            _effectRNG = data.EffectRNG;
            _effectDispatchCount = data.EffectDispatchCount;
        }

        public ChaosEffectActivationSignaler GetCurrentEffectSignaler()
        {
            if (!NetworkServer.active)
            {
                Log.Warning("Called on client");
                return null;
            }

            return _effectActivationSignalers.FirstOrDefault(s => s && s.enabled);
        }

        public void SkipAllScheduledEffects()
        {
            if (!NetworkServer.active)
            {
                Log.Warning("Called on client");
                return;
            }

            foreach (ChaosEffectActivationSignaler activationSignaler in _effectActivationSignalers)
            {
                if (activationSignaler && activationSignaler.enabled)
                {
                    activationSignaler.SkipAllScheduledEffects();
                }
            }
        }

        public void RewindEffectScheduling(float numSeconds)
        {
            if (!NetworkServer.active)
            {
                Log.Warning("Called on client");
                return;
            }

            foreach (ChaosEffectActivationSignaler activationSignaler in _effectActivationSignalers)
            {
                if (activationSignaler && activationSignaler.enabled)
                {
                    activationSignaler.RewindEffectScheduling(numSeconds);
                }
            }
        }

        public void DispatchEffectFromSerializedDataServer(ChaosEffectInfo effectInfo, byte[] serializedEffectData, in ChaosEffectDispatchArgs args = default)
        {
            if (!NetworkServer.active)
            {
                Log.Warning("Called on client");
                return;
            }

            ChaosEffectDispatchArgs dispatchArgs = args;
            dispatchArgs.DispatchFlags |= EffectDispatchFlags.DontStart | EffectDispatchFlags.SkipServerInit;
            BaseEffect effectInstance = dispatchEffectFromSerializedData(effectInfo, serializedEffectData, dispatchArgs);
            if (effectInstance != null)
            {
                NetworkReader reader = new NetworkReader(serializedEffectData);

                try
                {
                    effectInstance.Deserialize(reader);
                }
                catch (Exception ex)
                {
                    Log.Error_NoCallerPrefix($"Caught exception in {effectInfo} {nameof(BaseEffect.Deserialize)}: {ex}");
                    Chat.AddMessage(Language.GetString("CHAOS_EFFECT_UNHANDLED_EXCEPTION_MESSAGE"));
                    return;
                }

                if (effectInfo.IsNetworked)
                {
                    new NetworkedEffectDispatchedMessage(effectInfo, args, serializedEffectData).Send(NetworkDestination.Clients);
                }

                startEffect(effectInfo, args, effectInstance);
            }
        }

        BaseEffect dispatchEffectFromSerializedData(ChaosEffectInfo effectInfo, byte[] serializedEffectData, in ChaosEffectDispatchArgs args = default)
        {
            ChaosEffectDispatchArgs dispatchArgs = args;
            dispatchArgs.DispatchFlags |= EffectDispatchFlags.DontStart;

            BaseEffect effectInstance = dispatchEffect(effectInfo, dispatchArgs);
            if (effectInstance != null)
            {
                NetworkReader networkReader = new NetworkReader(serializedEffectData);

                try
                {
                    effectInstance.Deserialize(networkReader);
                }
                catch (Exception ex)
                {
                    Log.Error_NoCallerPrefix($"Caught exception in {effectInfo} {nameof(BaseEffect.Deserialize)}: {ex}");
                }

                if (!args.HasFlag(EffectDispatchFlags.DontStart))
                {
                    startEffect(effectInfo, args, effectInstance);
                }
            }

            return effectInstance;
        }

        void NetworkedEffectDispatchedMessage_OnReceive(ChaosEffectInfo effectInfo, in ChaosEffectDispatchArgs args, byte[] serializedEffectData)
        {
            if (NetworkServer.active)
                return;

            ChaosEffectDispatchArgs dispatchArgs = args;
            dispatchArgs.DispatchFlags |= EffectDispatchFlags.DontStart;

            BaseEffect effectInstance = dispatchEffectFromSerializedData(effectInfo, serializedEffectData, dispatchArgs);
            if (effectInstance != null)
            {
                startEffect(effectInfo, args, effectInstance);

#if DEBUG
                Log.Debug($"Started networked effect {effectInfo}");
#endif
            }
        }

        [ConCommand(commandName = "roc_startrandom", flags = ConVarFlags.SenderMustBeServer, helpText = "Dispatches a random effect")]
        static void CCDispatchRandomEffect(ConCommandArgs args)
        {
            if (!NetworkServer.active || !Run.instance || !_instance || !_instance.enabled)
                return;

            _instance.dispatchEffect(ChaosEffectCatalog.PickActivatableEffect(RoR2Application.rng, EffectCanActivateContext.Now));
        }

        [ConCommand(commandName = "roc_start", flags = ConVarFlags.SenderMustBeServer, helpText = "Dispatches an effect")]
        static void CCDispatchEffect(ConCommandArgs args)
        {
            if (!NetworkServer.active || !Run.instance || !_instance || !_instance.enabled)
                return;

            ChaosEffectIndex index = ChaosEffectCatalog.FindEffectIndex(args[0]);
            if (index > ChaosEffectIndex.Invalid)
            {
                _instance.dispatchEffect(ChaosEffectCatalog.GetEffectInfo(index), new ChaosEffectDispatchArgs
                {
                    DispatchFlags = EffectDispatchFlags.DontCount,
                    OverrideRNGSeed = args.Count > 1 ? args.GetArgULong(1) : null
                });
            }
        }

        void ActivationSignaler_SignalShouldDispatchEffect(ChaosEffectInfo effect, in ChaosEffectDispatchArgs args)
        {
            if (Configs.General.DisableEffectDispatching.Value)
                return;

            DispatchEffect(effect, args);
        }

        public void DispatchEffect(ChaosEffectInfo effect, in ChaosEffectDispatchArgs args = default)
        {
            if (!NetworkServer.active)
            {
                Log.Warning("Called on client");
                return;
            }

            dispatchEffect(effect, args);
        }

        BaseEffect dispatchEffect(ChaosEffectInfo effect, in ChaosEffectDispatchArgs args = default)
        {
            if (effect is null)
                throw new ArgumentNullException(nameof(effect));

            bool isServer = NetworkServer.active;
            if (!isServer && !effect.IsNetworked)
            {
                Log.Error($"Attempting to dispatch non-networked effect {effect} as client");
                return null;
            }

            if (isServer)
            {
                if (!args.HasFlag(EffectDispatchFlags.DontSendChatMessage))
                {
                    Chat.SendBroadcastChat(new Chat.SimpleChatMessage
                    {
                        baseToken = "CHAOS_EFFECT_ACTIVATE",
                        paramTokens = new string[] { effect.GetDisplayName() }
                    });
                }

                HasAttemptedDispatchAnyEffectServer = true;

                bool canActivate = !args.HasFlag(EffectDispatchFlags.CheckCanActivate) || effect.CanActivate(EffectCanActivateContext.Now);

                OnEffectAboutToDispatchServer?.Invoke(effect, args, canActivate);

                if (!canActivate)
                {
#if DEBUG
                    Log.Debug($"{effect} is not activatable, not starting");
#endif

                    return null;
                }
            }

            CreateEffectInstanceArgs createEffectArgs;
            if (isServer && !args.HasFlag(EffectDispatchFlags.SkipServerInit))
            {
                createEffectArgs = new CreateEffectInstanceArgs(_effectDispatchCount++, args.OverrideRNGSeed.GetValueOrDefault(_effectRNG.nextUlong));
            }
            else
            {
                // Clients will get the seed from the server in Deserialize
                createEffectArgs = CreateEffectInstanceArgs.None;
            }

            BaseEffect effectInstance = effect.CreateInstance(createEffectArgs);
            if (effectInstance != null)
            {
                if (isServer && !args.HasFlag(EffectDispatchFlags.SkipServerInit))
                {
                    try
                    {
                        effectInstance.OnPreStartServer();
                    }
                    catch (Exception ex)
                    {
                        Log.Error_NoCallerPrefix($"Caught exception in {effect} {nameof(BaseEffect.OnPreStartServer)}: {ex}");
                        Chat.AddMessage(Language.GetString("CHAOS_EFFECT_UNHANDLED_EXCEPTION_MESSAGE"));
                    }

                    if (effect.IsNetworked)
                    {
                        NetworkWriter networkWriter = new NetworkWriter();

                        try
                        {
                            effectInstance.Serialize(networkWriter);
                        }
                        catch (Exception ex)
                        {
                            Log.Error_NoCallerPrefix($"Caught exception in {effect} {nameof(BaseEffect.Serialize)}: {ex}");
                        }

                        new NetworkedEffectDispatchedMessage(effect, args, networkWriter.ToArray()).Send(NetworkDestination.Clients);
                    }
                }

                if (!args.HasFlag(EffectDispatchFlags.DontStart))
                {
                    startEffect(effect, args, effectInstance);
                }
            }

            return effectInstance;
        }

        void startEffect(ChaosEffectInfo effectInfo, in ChaosEffectDispatchArgs args, BaseEffect effectInstance)
        {
            try
            {
                effectInstance.OnStart();
            }
            catch (Exception ex)
            {
                Log.Error_NoCallerPrefix($"Caught exception in {effectInfo} {nameof(BaseEffect.OnStart)}: {ex}");
                Chat.AddMessage(Language.GetString("CHAOS_EFFECT_UNHANDLED_EXCEPTION_MESSAGE"));
            }

            OnEffectDispatched?.Invoke(effectInfo, args, effectInstance);
        }
    }
}
