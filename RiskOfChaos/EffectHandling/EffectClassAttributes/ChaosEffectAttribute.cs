﻿using BepInEx.Configuration;
using RiskOfChaos.EffectDefinitions;
using System;

namespace RiskOfChaos.EffectHandling.EffectClassAttributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class ChaosEffectAttribute : HG.Reflection.SearchableAttribute
    {
        public readonly string Identifier;

        public string ConfigName { get; set; } = null;

        public float DefaultSelectionWeight { get; set; } = 1f;

        public bool IsNetworked { get; set; } = false;

        public new Type target => base.target as Type;

        public ChaosEffectAttribute(string identifier)
        {
            Identifier = identifier;
        }

        internal virtual bool Validate()
        {
            if (target == null)
            {
                Log.Warning($"Invalid attribute target ({base.target})");
                return false;
            }

            if (!typeof(BaseEffect).IsAssignableFrom(target))
            {
                Log.Error($"Effect '{Identifier}' type ({target.FullName}) does not derive from {nameof(BaseEffect)}");
                return false;
            }

            return true;
        }

        public virtual ChaosEffectInfo BuildEffectInfo(ChaosEffectIndex index, ConfigFile configFile)
        {
            return new ChaosEffectInfo(index, this, configFile);
        }
    }
}
