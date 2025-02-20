﻿using BepInEx.Configuration;
using RiskOfChaos.EffectHandling;
using RiskOfOptions.OptionConfigs;
using RoR2;
using System;

namespace RiskOfChaos.ConfigHandling
{
    public abstract class ConfigHolderBase
    {
        public readonly string Key;
        public readonly ConfigDescription Description;
        public readonly ConfigFlags Flags;

        protected BaseOptionConfig _optionConfig;

        protected readonly string[] _previousKeys;
        protected string[] _previousConfigSectionNames;

        protected ConfigFile _configFile;

        public ConfigDefinition Definition { get; protected set; }

        public ConfigEntryBase Entry { get; protected set; }

        protected bool _hasServerOverrideValue = false;
        protected object _serverOverrideValue;

        public object LocalBoxedValue
        {
            get => Entry.BoxedValue;
            set => Entry.BoxedValue = value;
        }

        public object BoxedValue
        {
            get => _hasServerOverrideValue ? _serverOverrideValue : LocalBoxedValue;
            set => LocalBoxedValue = value;
        }

        public event EventHandler<ConfigChangedArgs> SettingChanged;

        public delegate void OnBindDelegate(ConfigEntryBase entry);
        public event OnBindDelegate OnBind;

        protected ConfigHolderBase(string key, ConfigDescription description, string[] previousKeys, string[] previousSections, ConfigFlags flags)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException($"'{nameof(key)}' cannot be null or empty.", nameof(key));

            Key = key;
            Description = description ?? throw new ArgumentNullException(nameof(description));
            _previousKeys = previousKeys ?? throw new ArgumentNullException(nameof(previousKeys));
            Flags = flags;

            Run.onRunDestroyGlobal += onRunEnd;
        }

        ~ConfigHolderBase()
        {
            Run.onRunDestroyGlobal -= onRunEnd;
        }

        public abstract void Bind(ChaosEffectInfo effectInfo);

        public abstract void Bind(ConfigFile file, string section, string modGuid = null, string modName = null);

        public void SetOptionConfig(BaseOptionConfig newConfig)
        {
            if (Entry != null)
                Log.Warning("Config already binded, setting config options will not work");

            _optionConfig = newConfig;
        }

        protected virtual void invokeSettingChanged()
        {
            SettingChanged?.Invoke(this, new ConfigChangedArgs(this));
        }

        protected virtual void invokeOnBind()
        {
            OnBind?.Invoke(Entry);
        }

        public void SetServerOverrideValue(object value)
        {
            _serverOverrideValue = value;
            _hasServerOverrideValue = true;

            invokeSettingChanged();
        }

        public void ClearServerOverrideValue()
        {
            _serverOverrideValue = null;
            _hasServerOverrideValue = false;

            invokeSettingChanged();
        }

        void onRunEnd(Run run)
        {
            ClearServerOverrideValue();
        }
    }
}
