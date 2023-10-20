﻿using BepInEx.Configuration;
using RiskOfChaos.ConfigHandling;
using RiskOfOptions.OptionConfigs;

namespace RiskOfChaos
{
    partial class Configs
    {
        public static class EffectSelection
        {
            public const string SECTION_NAME = "Effect Selection";

            public static readonly ConfigHolder<bool> SeededEffectSelection =
                ConfigFactory<bool>.CreateConfig("Seeded Effect Selection", false)
                                   .Description("If the effects should be consistent with the run seed, only really changes anything if you're setting run seeds manually")
                                   .OptionConfig(new CheckBoxConfig())
                                   .MovedFrom(General.SECTION_NAME)
                                   .Build();

            static bool perStageEffectListDisabled() => !PerStageEffectListEnabled.Value;

            public static readonly ConfigHolder<bool> PerStageEffectListEnabled =
                ConfigFactory<bool>.CreateConfig("Per-Stage Effect List", false)
                                   .Description("If enabled, a subsection of all effects is generated each stage and only effects from this list are activated.\nNot supported in any chat voting mode")
                                   .OptionConfig(new CheckBoxConfig())
                                   .Build();

            public static readonly ConfigHolder<int> PerStageEffectListSize =
                ConfigFactory<int>.CreateConfig("Effect List Size", 20)
                                  .Description("The size of the per-stage effect list\nNot supported in any chat voting mode")
                                  .OptionConfig(new IntSliderConfig
                                  {
                                      min = 1,
                                      max = 100,
                                      checkIfDisabled = perStageEffectListDisabled
                                  })
                                  .Build();

            internal static void Bind(ConfigFile file)
            {
                void bindConfig<T>(ConfigHolder<T> config)
                {
                    config.Bind(file, SECTION_NAME, CONFIG_GUID, CONFIG_NAME);
                }

                bindConfig(SeededEffectSelection);

                bindConfig(PerStageEffectListEnabled);

                bindConfig(PerStageEffectListSize);
            }
        }
    }
}
