using ElRaccoone.Tweens;
using ElRaccoone.Tweens.Core;
using Player;
using UI.MainMenu;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class CharacterSettings : MonoBehaviour
    {
        public Transform ScreenPointTarget;
        private Camera _cam;
        private CanvasScaler _canvasScaler;
        private CanvasGroup _canvasGroup;

        private CharacterParts _parts;

        public CharacterPartSwitcher CharacterSwitcher;
        public CharacterPartSwitcher MaterialSwitcher;
        public CharacterPartSwitcher SkinToneSwitcher;
        public CharacterPartSwitcher HairSwitcher;
        public CharacterPartSwitcher HatSwitcher;

        private void Awake()
        {
            _cam = Camera.main;
            _canvasScaler = GetComponentInParent<CanvasScaler>();
            _canvasGroup = GetComponent<CanvasGroup>();
            
            _parts = FindObjectOfType<CharacterParts>();

            var menu = transform.parent.GetComponentInChildren<Menu>();
            menu.OnPageChange.AddListener(main =>
            {
                _canvasGroup.TweenCanvasGroupAlpha(main ? 1f : 0f, 0.1f)
                    .SetEase(EaseType.CubicInOut);
            });

            var selectedParts = _parts.GetPartsFromPrefs();
            
            // Character model
            CharacterSwitcher.Index = selectedParts.CharacterIndex;
            CharacterSwitcher.MaxIndex = _parts.Characters.Length - 1;
            CharacterSwitcher.OnIndexChange.AddListener(i =>
            {
                PlayerPrefs.SetInt("CharacterIndex", i);
                SetActiveCharacterPart(ref _parts.Characters, i);
            });
            
            // Outfit + skin tone
            // Each outfit has 3 materials (skin tones variants)
            void SetAllCharacterMaterials(int _)
            {
                var offsetIndex = MaterialSwitcher.Index * 3 + SkinToneSwitcher.Index;
                PlayerPrefs.SetInt("MaterialIndex", offsetIndex);
                foreach (var character in _parts.Characters) SetMaterial(character, ref _parts.Materials, offsetIndex);
            }

            var materialIndex = selectedParts.MaterialIndex;
            
            MaterialSwitcher.Index = (materialIndex - materialIndex % 3) / 3;
            MaterialSwitcher.MaxIndex = _parts.Materials.Length / 3 - 1;
            MaterialSwitcher.OnIndexChange.AddListener(SetAllCharacterMaterials);
            
            SkinToneSwitcher.Index = materialIndex % 3;
            SkinToneSwitcher.MaxIndex = 2;
            SkinToneSwitcher.OnIndexChange.AddListener(SetAllCharacterMaterials);
            
            HairSwitcher.Index = selectedParts.HairIndex;
            HairSwitcher.MaxIndex = _parts.Hairs.Length - 1;
            HairSwitcher.OnIndexChange.AddListener(i =>
            {
                PlayerPrefs.SetInt("HairIndex", i);
                SetActiveCharacterPart(ref _parts.Hairs, i);
            });

            HatSwitcher.Index = selectedParts.HatIndex;
            HatSwitcher.MaxIndex = _parts.Hats.Length - 1;
            HatSwitcher.OnIndexChange.AddListener(i =>
            {
                PlayerPrefs.SetInt("HatIndex", i);
                SetActiveCharacterPart(ref _parts.Hats, i, true);
            });
        }

        private void Start()
        {
            _parts.Apply(_parts.GetPartsFromPrefs());
        }
        
        public static void SetMaterial(Transform characterGeometry, ref Material[] materials, int index)
        {
            foreach (var smr in characterGeometry.GetComponentsInChildren<SkinnedMeshRenderer>())
                smr.material = materials[index];
        }

        public static void SetActiveCharacterPart(ref Transform[] array, int index, bool allowNone = false)
        {
            index = Mathf.Clamp(index, allowNone ? -1 : 0, array.Length - 1);
            for (var i = 0; i < array.Length; i++) array[i].gameObject.SetActive(i == index);
        }

        private void LateUpdate()
        {
            var pos = _cam.WorldToScreenPoint(ScreenPointTarget.position);
            transform.position = new Vector2(pos.x, pos.y) / _canvasScaler.scaleFactor;
        }
    }
}
