﻿namespace RiskOfChaos.ModifierController.Pickups
{
    public class PickupModificationManager : ValueModificationManager<PickupModificationInfo>
    {
        static PickupModificationManager _instance;
        public static PickupModificationManager Instance => _instance;

        protected override void OnEnable()
        {
            base.OnEnable();

            SingletonHelper.Assign(ref _instance, this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            SingletonHelper.Unassign(ref _instance, this);
        }

        public uint BounceCount { get; private set; }

        public override PickupModificationInfo InterpolateValue(in PickupModificationInfo a, in PickupModificationInfo b, float t, ValueInterpolationFunctionType interpolationType)
        {
            return PickupModificationInfo.Interpolate(a, b, t, interpolationType);
        }

        public override void UpdateValueModifications()
        {
            PickupModificationInfo modificationInfo = GetModifiedValue(new PickupModificationInfo());
            BounceCount = modificationInfo.BounceCount;
        }
    }
}
