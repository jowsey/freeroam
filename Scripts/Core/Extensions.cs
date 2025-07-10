using System;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using Weapons;
using Attribute = Epic.OnlineServices.Lobby.Attribute;

namespace Core
{
    public static class Extensions
    {
        public static Vector3 Flatten(this Vector3 vector) => new(vector.x, 0, vector.z);

        public static string ToUtf8Str(this Attribute value) => value.Data.Value.AsUtf8;

        public static string GetAttr(this List<Attribute> attributes, string attr) =>
            attributes.Find(x => string.Equals(x.Data.Key, attr, StringComparison.CurrentCultureIgnoreCase)).ToUtf8Str();

        public static Weapon InstantiateFromType(this WeaponType type, Transform parent) =>
            new WeaponData
            {
                Type = type
            }.InstantiateFromData(parent);
        
        public static IEnumerable<CinemachineOrbitalTransposer> GetAllRigs(this CinemachineFreeLook freeLook)
        {
            var i = 0;
            var rigs = new List<CinemachineOrbitalTransposer>();
            
            while (true)
            {
                var rig = freeLook.GetRig(i);
                if (rig == null) break;
                
                rigs.Add(rig.GetCinemachineComponent<CinemachineOrbitalTransposer>());
                i++;
            }

            return rigs;
        }
    }
}
