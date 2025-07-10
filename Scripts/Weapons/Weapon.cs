using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using ElRaccoone.Tweens;
using ElRaccoone.Tweens.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace Weapons
{
    public enum HitmarkerType
    {
        Damage,
        Kill
    }

    public enum WeaponType
    {
        None,
        AssaultRifle,
        CombatPistol,
    }

    [Serializable]
    public struct WeaponPositions
    {
        public Vector3 downPosition;
        public Vector3 downRotation;

        public Vector3 aimPosition;
        // public Vector3 aimRotation; // not needed, animation rigging overwrites
    }

    /// <summary>
    /// Weapon data associated with a weapon type. Ideally doesn't change at runtime
    /// </summary>
    public class Weapon : MonoBehaviour
    {
        [FormerlySerializedAs("_weaponPositions")]
        public WeaponPositions Positions;

        public Transform LeftHandIKTargetDown;
        public Transform RightHandIKTargetDown;

        public Transform LeftHandIKTargetAim;
        public Transform RightHandIKTargetAim;

        [Tooltip("Rate of bullets fired per minute")]
        public int RoundsPerMinute;

        [Tooltip("Seconds between hitting reload and being able to fire again")]
        public float ReloadTime;

        [Tooltip("Damage dealt per hit")]
        public float Damage;

        [Tooltip("Range at which damage begins to drop off. Hits are all 100% damage before this")]
        public float DamageFalloffBegin;

        [Tooltip("Range at which damage will be DamageFalloffPercent of original damage")]
        public float DamageFalloffEnd;

        [Tooltip("Percent of original damage every hit will do by DamageFalloffEnd")]
        public float DamageFalloffPercent;

        [Tooltip("Amount of recoil per shot")]
        public Vector2 Recoil;

        [Tooltip("Muzzle flash particles")]
        public ParticleSystem MuzzleFlashFX;

        [Tooltip("Magazine object")]
        public GameObject Magazine;

        [Tooltip("Position that reload anim targets when moving magazines")]
        [NonSerialized]
        public Vector3 MagazineLocalPos;

        [Tooltip("Magazine ammunition capacity")]
        public int MaxAmmo;

        [HideInInspector]
        public double LastFiredTimestamp;

        public float TimeBetweenShots => 60f / ModifiedRoundsPerMinute;

        [Tooltip("Reload state data")]
        public readonly WeaponReloadStateData ReloadStateData = new();

        public List<ModificationCategory> AttachmentCategories;

        [Tooltip("Weapon data")]
        public WeaponData WeaponData;

        public IEnumerable<WeaponModData> ActiveAttachments => AttachmentCategories
            .Select(ctgry => ctgry.SelectedIndex == -1 ? null : ctgry.Attachments[ctgry.SelectedIndex].GetComponent<WeaponModData>())
            .Where(attachment => attachment != null).ToArray();

        public float ModifiedDamage => ActiveAttachments.Aggregate(Damage, (current, attachment) => current * attachment.DamageModifier);
        public float ModifiedDamageFalloffBegin => ActiveAttachments.Aggregate(DamageFalloffBegin, (current, attachment) => current * attachment.FalloffRangeModifier);
        public float ModifiedDamageFalloffEnd => ActiveAttachments.Aggregate(DamageFalloffEnd, (current, attachment) => current * attachment.FalloffRangeModifier);
        public float ModifiedRoundsPerMinute => ActiveAttachments.Aggregate(RoundsPerMinute, (current, attachment) => Mathf.RoundToInt(current * attachment.FireRateModifier));
        public float ModifiedReloadTime => ActiveAttachments.Aggregate(ReloadTime, (current, attachment) => current * attachment.ReloadSpeedModifier);
        public Vector2 ModifiedRecoil => ActiveAttachments.Aggregate(Recoil, (current, attachment) => current * attachment.RecoilModifier);

        public int ActiveSkin = -1;

        private void Awake()
        {
            MagazineLocalPos = Magazine.transform.localPosition;
        }

        public void ApplySkin(int skinIndex)
        {
            if(ActiveSkin == skinIndex) return;

            ActiveSkin = skinIndex;
            foreach (var rndr in GetComponentsInChildren<Renderer>())
            {
                // only apply weapon skins to objects that already have a weapon skin
                if(!WeaponSkins.Skins.Select(skin => skin.name).Contains(rndr.material.name.Replace(" (Instance)", ""))) continue;

                rndr.material = WeaponSkins.Skins[skinIndex];
            }
        }

        public void ApplyAttachments(int[] indexes)
        {
            // fill indexes with -1 if not enough
            if(indexes.Length < AttachmentCategories.Count) indexes = indexes.Concat(Enumerable.Repeat(-1, AttachmentCategories.Count - indexes.Length)).ToArray();

            for (var i = 0; i < AttachmentCategories.Count; i++)
            {
                var category = AttachmentCategories[i];

                if(category.SelectedIndex == indexes[i]) continue;
                category.SelectedIndex = indexes[i];

                // disable all attachments
                foreach (var attachment in category.Attachments) attachment.SetActive(false);

                // enable selected attachment
                if(indexes[i] == -1)
                {
                    if(category.UseFirstAsDefault) category.Attachments[0].SetActive(true);
                }
                else
                {
                    category.Attachments[indexes[i]].SetActive(true);
                }
            }
        }

        /// <summary>
        /// Client-side cosmetic FX for firing
        /// </summary>
        public async void Fire(Vector3 hitPoint)
        {
            MuzzleFlashFX.Play();

            // Tween weapon kickback
            gameObject.TweenCancelAll();
            gameObject.TweenLocalPositionZ(-0.035f, 0.075f)
                .SetEase(EaseType.ExpoOut)
                .SetPingPong()
                .SetFrom(0);

            gameObject.TweenLocalRotationX(-2f, 0.075f)
                .SetEase(EaseType.ExpoOut)
                .SetPingPong()
                .SetFrom(0);

            // Play gunshot 
            // todo give own audio source
            // todo add audio from inspector for different ammo/gun types
            var clip = Addressables.LoadAssetAsync<AudioClip>("Audio/Gunshot").WaitForCompletion();
            AudioSource.PlayClipAtPoint(clip, transform.position, Random.Range(0.8f, 1f));

            // Create bullet trail
            var bulletTrail = (await Addressables.InstantiateAsync("FX/BulletTrail").Task).GetComponent<LineRenderer>();
            bulletTrail.SetPosition(0, MuzzleFlashFX.transform.position);
            bulletTrail.SetPosition(1, hitPoint);

            // Fade out bullet trail transparency
            bulletTrail.TweenValueColor(Color.clear, 0.15f, val => bulletTrail.startColor = bulletTrail.endColor = val)
                .SetFrom(Color.white * 0.5f)
                .SetEase(EaseType.CubicOut)
                .SetOnComplete(() => Destroy(bulletTrail.gameObject));
        }

        public string GetWeaponName() => WeaponTypeToName(WeaponData.Type);

        public string GetWeaponAddressable() => WeaponTypeToAddressable(WeaponData.Type);

        public static string WeaponTypeToName(WeaponType type)
        {
            return type switch
            {
                WeaponType.None => null,
                WeaponType.AssaultRifle => "Assault Rifle",
                WeaponType.CombatPistol => "Combat Pistol",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string WeaponTypeToAddressable(WeaponType type)
        {
            return type switch
            {
                WeaponType.None => null,
                WeaponType.AssaultRifle => "Weapons/AssaultRifle",
                WeaponType.CombatPistol => "Weapons/CombatPistol",
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}
