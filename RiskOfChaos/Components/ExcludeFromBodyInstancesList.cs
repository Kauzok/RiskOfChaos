﻿using RoR2;
using UnityEngine;

namespace RiskOfChaos.Components
{
    // Excludes a body from the instances list, prevents the game assuming it is a valid character
    // If using this component, you will be responsible for cleaning up the object whenever it is no longer is use
    [RequireComponent(typeof(CharacterBody))]
    public class ExcludeFromBodyInstancesList : MonoBehaviour
    {
        [SystemInitializer]
        static void Init()
        {
            On.RoR2.CharacterBody.OnEnable += (orig, self) =>
            {
                orig(self);

                if (self.GetComponent<ExcludeFromBodyInstancesList>())
                {
#pragma warning disable Publicizer001 // Accessing a member that was not originally public
                    CharacterBody.instancesList.Remove(self);
#pragma warning restore Publicizer001 // Accessing a member that was not originally public
                }
            };
        }
    }
}
