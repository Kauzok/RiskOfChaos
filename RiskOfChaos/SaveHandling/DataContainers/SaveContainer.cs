﻿using RiskOfChaos.SaveHandling.DataContainers.EffectHandlerControllers;
using System;
using System.Runtime.Serialization;

namespace RiskOfChaos.SaveHandling.DataContainers
{
    [Serializable]
    public class SaveContainer
    {
        [DataMember(Name = "eas")]
        public EffectActivationSignalerData ActivationSignalerData;

        [DataMember(Name = "eac")]
        public EffectActivationCounterData ActivationCounterData;

        [DataMember(Name = "ed")]
        public EffectDispatcherData DispatcherData;

        [DataMember(Name = "ted")]
        public TimedEffectHandlerData TimedEffectHandlerData;

        [DataMember(Name = "e")]
        public EffectsDataContainer Effects;

        public static SaveContainer CreateEmpty()
        {
            return new SaveContainer
            {
                ActivationSignalerData = new EffectActivationSignalerData(),
                ActivationCounterData = new EffectActivationCounterData(),
                DispatcherData = new EffectDispatcherData(),
                Effects = new EffectsDataContainer()
            };
        }
    }
}