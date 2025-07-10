using UnityEngine;

namespace Weapons
{
    public class WeaponReloadStateData
    {
        [Tooltip("Is weapon in the middle of an unfinished reload")]
        public bool IsMidReload;

        /// <summary>
        /// New magazine that has been instantiated but has not yet reached the weapon
        /// </summary>
        public GameObject NewMagazine;
    }
}
