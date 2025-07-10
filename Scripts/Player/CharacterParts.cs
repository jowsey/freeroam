using System;
using UI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Player
{
    [Serializable]
    public struct PartIndexes
    {
        public int CharacterIndex;
        public int MaterialIndex;
        public int HairIndex;
        public int HatIndex;
    }
    
    public class CharacterParts : MonoBehaviour
    {
        public Transform[] Characters;
        public Material[] Materials;
        public Transform[] Hairs;
        public Transform[] Hats;

        public PartIndexes GetPartsFromPrefs() => new()
        {
            CharacterIndex = PlayerPrefs.GetInt("CharacterIndex", Random.Range(0, 2)),
            MaterialIndex = PlayerPrefs.GetInt("MaterialIndex", Random.Range(0, 3)),
            HairIndex = PlayerPrefs.GetInt("HairIndex", 0),
            HatIndex = PlayerPrefs.GetInt("HatIndex", -1)
        };

        public void Apply(PartIndexes indexes)
        {
            CharacterSettings.SetActiveCharacterPart(ref Characters, indexes.CharacterIndex);
            CharacterSettings.SetMaterial(transform, ref Materials, indexes.MaterialIndex);
            CharacterSettings.SetActiveCharacterPart(ref Hairs, indexes.HairIndex);
            CharacterSettings.SetActiveCharacterPart(ref Hats, indexes.HatIndex, true);
        }
    }
}
