﻿using System;

namespace RiskOfChaos.ModifierController
{
    public interface IValueModificationManager<TValue>
    {
        event Action OnValueModificationUpdated;

        bool AnyModificationActive { get; }

        void RegisterModificationProvider(IValueModificationProvider<TValue> provider);

        void UnregisterModificationProvider(IValueModificationProvider<TValue> provider);
    }
}
