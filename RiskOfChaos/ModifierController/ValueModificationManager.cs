﻿using RoR2;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine.Networking;

namespace RiskOfChaos.ModifierController
{
    public abstract class ValueModificationManager<TModificationProvider, TValue> : NetworkBehaviour where TModificationProvider : IValueModificationProvider<TValue>
    {
        protected readonly HashSet<TModificationProvider> _modificationProviders = new HashSet<TModificationProvider>();

        const uint ANY_MODIFICATION_ACTIVE_DIRTY_BIT = 1 << 0;

        bool _anyModificationActive;
        public bool AnyModificationActive
        {
            get
            {
                return _anyModificationActive;
            }

            [param: In]
            private set
            {
                if (NetworkServer.localClientActive && !syncVarHookGuard)
                {
                    syncVarHookGuard = true;
                    syncAnyModificationActive(value);
                    syncVarHookGuard = false;
                }

                SetSyncVar(value, ref _anyModificationActive, ANY_MODIFICATION_ACTIVE_DIRTY_BIT);
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            syncAnyModificationActive(_anyModificationActive);
        }

        protected virtual void syncAnyModificationActive(bool active)
        {
            AnyModificationActive = active;
        }

        bool _modificationProvidersDirty;

        protected void onModificationProviderDirty()
        {
            if (!NetworkServer.active)
            {
                Log.Warning("Called on client");
                return;
            }

            if (_modificationProvidersDirty)
                return;

            RoR2Application.onNextUpdate += updateValueModifiers;

            _modificationProvidersDirty = true;
        }

        public void RegisterModificationProvider(TModificationProvider provider)
        {
            if (!NetworkServer.active)
            {
                Log.Warning("Called on client");
                return;
            }

            if (_modificationProviders.Add(provider))
            {
                provider.OnValueDirty += onModificationProviderDirty;
                onModificationProviderDirty();
            }
        }

        public void UnregisterModificationProvider(TModificationProvider provider)
        {
            if (!NetworkServer.active)
            {
                Log.Warning("Called on client");
                return;
            }

            if (_modificationProviders.Remove(provider))
            {
                provider.OnValueDirty -= onModificationProviderDirty;
                onModificationProviderDirty();
            }
        }

        void updateValueModifiers()
        {
            _modificationProvidersDirty = false;

            if (!NetworkServer.active)
            {
                Log.Warning("Called on client");
                return;
            }

            AnyModificationActive = _modificationProviders.Count > 0;

            updateValueModifications();
        }

        protected abstract void updateValueModifications();

        protected virtual TValue getModifiedValue(TValue baseValue)
        {
            foreach (TModificationProvider modificationProvider in _modificationProviders)
            {
                modificationProvider.ModifyValue(ref baseValue);
            }

            return baseValue;
        }

        public override sealed bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            uint dirtyBits = syncVarDirtyBits;
            if (!initialState)
            {
                writer.WritePackedUInt32(dirtyBits);
            }

            return serialize(writer, initialState, dirtyBits);
        }

        protected virtual bool serialize(NetworkWriter writer, bool initialState, uint dirtyBits)
        {
            if (initialState)
            {
                writer.Write(_anyModificationActive);
                return true;
            }

            bool anythingWritten = false;

            if ((dirtyBits & ANY_MODIFICATION_ACTIVE_DIRTY_BIT) != 0)
            {
                writer.Write(_anyModificationActive);
                anythingWritten = true;
            }

            return anythingWritten;
        }

        public override sealed void OnDeserialize(NetworkReader reader, bool initialState)
        {
            deserialize(reader, initialState, initialState ? ~0b0U : reader.ReadPackedUInt32());
        }

        protected virtual void deserialize(NetworkReader reader, bool initialState, uint dirtyBits)
        {
            if (initialState)
            {
                _anyModificationActive = reader.ReadBoolean();
                return;
            }

            if ((dirtyBits & ANY_MODIFICATION_ACTIVE_DIRTY_BIT) != 0)
            {
                syncAnyModificationActive(reader.ReadBoolean());
            }
        }
    }
}