using Core;
using UnityEngine;

namespace Weapons
{
    public class WeaponModData : ModificationData
    {
        public float DamageModifier = 1f;
        public float FalloffRangeModifier = 1f;
        public float FireRateModifier = 1f;
        public float ReloadSpeedModifier = 1f;
        public Vector2 RecoilModifier = Vector2.one;
    }
}
