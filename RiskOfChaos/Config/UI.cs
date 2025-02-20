﻿using BepInEx.Configuration;
using RiskOfChaos.ConfigHandling;
using RiskOfChaos.Trackers;
using RiskOfOptions.OptionConfigs;
using RoR2;
using RoR2.UI;
using System;
using UnityEngine;

namespace RiskOfChaos
{
    partial class Configs
    {
        public static class UI
        {
            public const string SECTION_NAME = "UI";

            public static readonly ConfigHolder<bool> HideActiveEffectsPanel =
                ConfigFactory<bool>.CreateConfig("Hide Active Effects Panel", false)
                                   .Description("Hides the active effects list under the Objectives display")
                                   .OptionConfig(new CheckBoxConfig())
                                   .Build();

            static bool activeEffectsPanelHidden() => HideActiveEffectsPanel.Value;

            public static readonly ConfigHolder<Color> ActiveEffectsTextColor =
                ConfigFactory<Color>.CreateConfig("Active Effect Text Color", Color.white)
                                    .Description("The color of the effect names in the \"Active Effects\" list")
                                    .OptionConfig(new ColorOptionConfig
                                    {
                                        checkIfDisabled = activeEffectsPanelHidden
                                    })
                                    .MovedFrom(General.SECTION_NAME)
                                    .Build();

            public static readonly ConfigHolder<bool> DisplayNextEffect =
                ConfigFactory<bool>.CreateConfig("Display Next Effect", true)
                                   .Description("Displays the next effect that will happen.\nOnly works if chat voting is disabled and seeded mode is enabled")
                                   .OptionConfig(new CheckBoxConfig())
                                   .Build();

            public enum NextEffectTimerDisplayType : byte
            {
                Never,
                WhenRunTimerUnavailable,
                Always
            }

            public static readonly ConfigHolder<NextEffectTimerDisplayType> NextEffectTimerDisplayMode =
                ConfigFactory<NextEffectTimerDisplayType>.CreateConfig("Next Effect Timer Display Mode", NextEffectTimerDisplayType.WhenRunTimerUnavailable)
                                                         .Description($"Displays how much time is left until the next effect.\n\n{NextEffectTimerDisplayType.Never}: The time remaining is never displayed.\n{NextEffectTimerDisplayType.WhenRunTimerUnavailable}: Displays time remaining only when the regular run timer is paused or otherwise not visible.\n{NextEffectTimerDisplayType.Always}: Time remaining is always displayed")
                                                         .OptionConfig(new ChoiceConfig())
                                                         .ValueValidator(CommonValueValidators.DefinedEnumValue<NextEffectTimerDisplayType>())
                                                         .Build();

            public static bool ShouldShowNextEffectTimer(HUD hud)
            {
                switch (NextEffectTimerDisplayMode.Value)
                {
                    case NextEffectTimerDisplayType.Never:
                        return false;
                    case NextEffectTimerDisplayType.WhenRunTimerUnavailable:
                        Run run = Run.instance;
                        return run && ((run.isRunStopwatchPaused && General.RunEffectsTimerWhileRunTimerPaused.Value) || !RunTimerUITracker.IsAnyTimerVisibleForHUD(hud));
                    case NextEffectTimerDisplayType.Always:
                        return true;
                    default:
                        throw new NotImplementedException();
                }
            }

            internal static void Bind(ConfigFile file)
            {
                void bindConfig<T>(ConfigHolder<T> config)
                {
                    config.Bind(file, SECTION_NAME, CONFIG_GUID, CONFIG_NAME);
                }

                bindConfig(HideActiveEffectsPanel);

                bindConfig(ActiveEffectsTextColor);

                bindConfig(DisplayNextEffect);

                bindConfig(NextEffectTimerDisplayMode);
            }
        }
    }
}
