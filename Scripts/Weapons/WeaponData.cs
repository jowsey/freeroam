using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Weapons
{
    public static class WeaponSkins
    {
        public static readonly List<Material> Skins;

        static WeaponSkins()
        {
            // load all addressables with label WeaponSkins
            Skins = Addressables.LoadAssetsAsync<Material>("WeaponSkin", null)
                .WaitForCompletion()
                .OrderBy(x => x.name)
                .ToList();
        }
    }

    /// <summary>
    /// Data specific to a runtime instance of a weapon
    /// </summary>
    [Serializable]
    public class WeaponData
    {
        public WeaponType Type;
        
        public int CurrentAmmo;

        public int SkinIndex;
        public int[] AttachmentIndexes = {};

        /// <summary>
        /// Create a weapon object from this data
        /// </summary>
        /// <param name="parent">Parent of object</param>
        /// <returns>Created weapon</returns>
        public Weapon InstantiateFromData(Transform parent)
        {
            var weapon = Addressables.InstantiateAsync(Weapon.WeaponTypeToAddressable(Type), parent).WaitForCompletion().GetComponent<Weapon>();
            weapon.WeaponData = this;
            
            weapon.ApplySkin(SkinIndex);
            weapon.ApplyAttachments(AttachmentIndexes);

            return weapon;
        }
    }
}
