﻿using RiskOfChaos.ConfigHandling;
using RiskOfChaos.EffectHandling.EffectClassAttributes;
using RiskOfChaos.EffectHandling.EffectClassAttributes.Data;
using RiskOfChaos.Utilities;
using RiskOfChaos.Utilities.Extensions;
using RiskOfOptions.OptionConfigs;
using RoR2;

namespace RiskOfChaos.EffectDefinitions.Character
{
    [ChaosEffect("freeze_all")]
    public sealed class FreezeAll : BaseEffect
    {
        [EffectConfig]
        static readonly ConfigHolder<float> _freezeDuration =
            ConfigFactory<float>.CreateConfig("Freeze Duration", 4f)
            .Description("How long all characters will be frozen for")
            .OptionConfig(new StepSliderConfig
            {
                min = 1f,
                max = 10f,
                increment = 1f
            })
            .ValueConstrictor(CommonValueConstrictors.GreaterThanOrEqualTo(1f))
            .Build();

        public override void OnStart()
        {
            CharacterBody.readOnlyInstancesList.TryDo(body =>
            {
                if (body && body.TryGetComponent(out SetStateOnHurt setStateOnHurt))
                {
                    ref bool canBeFrozen = ref setStateOnHurt.canBeFrozen;
                    bool originalCanBeFrozen = canBeFrozen;

                    canBeFrozen = true;
                    setStateOnHurt.SetFrozen(_freezeDuration.Value);
                    canBeFrozen = originalCanBeFrozen;
                }
            }, FormatUtils.GetBestBodyName);
        }
    }
}
