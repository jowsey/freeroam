using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core;
using ElRaccoone.Tweens;
using ElRaccoone.Tweens.Core;
using Mirror;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;
using UnityEngine.UI;
using Vehicles;
using Weapons;

namespace UI
{
    public enum MenuType
    {
        Weapon,
        Vehicle
    }

    public class AttachmentsMenu : MonoBehaviour
    {
        public bool IsOpenSelf;

        public static bool IsOpen => FindObjectsByType<AttachmentsMenu>(FindObjectsSortMode.None).Any(m => m.IsOpenSelf);

        [SerializeField]
        private MenuType _menuType;

        private PlayerInput _playerInput;

        public GameObject AttachmentButtonPrefab;
        public GameObject AttachmentMenuCategoryPrefab;

        public Transform CategoryContainer;

        public TextMeshProUGUI SkinCountText;

        private static PlayerLoadoutManager _lm => PlayerController.LocalPlayer.LoadoutManager;
        private static NetworkVehicle _nv => PlayerController.LocalPlayer.CurrentVehicle.GetComponent<NetworkVehicle>();

        private static int _currentWeaponSkinIndex =>
            WeaponSkins.Skins
                .Select(s => s.name)
                .ToList()
                .IndexOf(_lm.CurrentWeapon.GetComponent<Renderer>().material.name.Replace(" (Instance)", ""));

        private static int _currentVehicleSkinIndex =>
            VehicleSkins.Skins
                .Select(s => s.name)
                .ToList()
                .IndexOf(_nv.GetComponentInChildren<Renderer>().material.name.Replace(" (Instance)", ""));

        private int _currentSkinIndex => _menuType == MenuType.Weapon ? _currentWeaponSkinIndex : _currentVehicleSkinIndex;

        private List<Material> _skins => _menuType == MenuType.Weapon ? WeaponSkins.Skins : VehicleSkins.Skins;

        private bool _disallowOpenWeapon => PauseMenu.IsPaused
                                            || ChatBox.Instance.InputField.isFocused
                                            || !_lm.WeaponShown
                                            || _lm.Unarmed
                                            || PlayerController.LocalPlayer.CurrentState is PlayerStateDead;

        private bool _disallowOpenVehicle => PauseMenu.IsPaused
                                             || ChatBox.Instance.InputField.isFocused
                                             || PlayerController.LocalPlayer.CurrentVehicle != transform.GetComponentInParent<NetworkIdentity>()
                                             || PlayerController.LocalPlayer.CurrentState is not PlayerStateDriver;

        private bool _disallowOpen => _menuType == MenuType.Weapon ? _disallowOpenWeapon : _disallowOpenVehicle;

        private IEnumerator Start()
        {
            GetComponent<CanvasGroup>().alpha = 0f;

            // wait for network stuff to finish connecting & spawning player 
            while (!PlayerController.LocalPlayer)
                yield return null;

            _playerInput = PlayerController.LocalPlayer.PlayerInput;
            _playerInput.actions["Reload"].performed += ToggleMenu;

            gameObject.SetActive(false);
            IsOpenSelf = false;
        }

        private void Update()
        {
            // close menu if open and condition met
            if(IsOpenSelf && (_playerInput.actions["Reload"].WasReleasedThisFrame() || _disallowOpen)) ToggleMenu();
        }

        private void UpdateSkinIndexText() => SkinCountText.text = $"{_currentSkinIndex + 1}/{_skins.Count}";

        public void CycleSkinBackward()
        {
            if(_menuType == MenuType.Vehicle)
                _nv.CmdSetSkinIndex(_currentVehicleSkinIndex - 1 < 0 ? VehicleSkins.Skins.Count - 1 : _currentVehicleSkinIndex - 1);
            else
                _lm.CmdSetSkinIndex(_lm.ActiveLoadoutSlot, _currentWeaponSkinIndex - 1 < 0 ? WeaponSkins.Skins.Count - 1 : _currentWeaponSkinIndex - 1);

            UpdateSkinIndexText();
        }

        public void CycleSkinForward()
        {
            if(_menuType == MenuType.Vehicle)
                _nv.CmdSetSkinIndex((_currentVehicleSkinIndex + 1) % VehicleSkins.Skins.Count);
            else
                _lm.CmdSetSkinIndex(_lm.ActiveLoadoutSlot, (_currentWeaponSkinIndex + 1) % WeaponSkins.Skins.Count);

            UpdateSkinIndexText();
        }

        private void ToggleMenu(InputAction.CallbackContext context)
        {
            if(context.interaction is HoldInteraction) ToggleMenu();
        }

        private void ToggleMenu()
        {
            if(_playerInput == null || this == null) return; // stupid unity event stuff
            gameObject.TweenCancelAll();

            if(!IsOpenSelf) // open
            {
                if(_disallowOpen) return;
                IsOpenSelf = true;

                Cursor.lockState = CursorLockMode.None;

                UpdateSkinIndexText();

                gameObject.SetActive(true);

                foreach (Transform child in CategoryContainer)
                    Destroy(child.gameObject);

                var categories = _menuType == MenuType.Weapon
                    ? _lm.CurrentWeapon.AttachmentCategories
                    : _nv.ModificationCategories;

                foreach (var category in categories)
                {
                    var categoryContainer = Instantiate(AttachmentMenuCategoryPrefab, CategoryContainer).GetComponent<AttachmentMenuCategory>();
                    categoryContainer.CategoryNameText.text = category.Name;

                    // add None button
                    if(!category.UseFirstAsDefault)
                    {
                        var attachmentButton = Instantiate(AttachmentButtonPrefab, categoryContainer.ListContainer).GetComponent<AttachmentButton>();
                        attachmentButton.AttachmentNameText.text = "None";

                        if(category.SelectedIndex == -1) attachmentButton.GetComponentInChildren<TextMeshProUGUI>().color = Color.red;

                        attachmentButton.GetComponent<Button>().onClick.AddListener(() =>
                        {
                            foreach (var text in categoryContainer.GetComponentsInChildren<TextMeshProUGUI>()) text.color = Color.white;
                            attachmentButton.GetComponentInChildren<TextMeshProUGUI>().color = Color.red;

                            if(_menuType == MenuType.Vehicle)
                            {
                                _nv.CmdSetAttachmentCategoryIndex(categoryContainer.transform.GetSiblingIndex(), -1);
                            }
                            else
                            {
                                _lm.CmdSetAttachmentCategoryIndex(_lm.ActiveLoadoutSlot, categoryContainer.transform.GetSiblingIndex(), -1);
                            }
                        });
                    }

                    // add buttons for each attachment
                    foreach (var attachment in category.Attachments)
                    {
                        var attachmentButton = Instantiate(AttachmentButtonPrefab, categoryContainer.ListContainer).GetComponent<AttachmentButton>();
                        attachmentButton.AttachmentNameText.text = attachment.GetComponent<ModificationData>().Name;

                        // selected if index matches or usefirstasdefault and none selected
                        if(category.SelectedIndex == attachment.transform.GetSiblingIndex() ||
                           (attachment.transform.GetSiblingIndex() == 0 && category.UseFirstAsDefault && category.SelectedIndex == -1))
                            attachmentButton.GetComponentInChildren<TextMeshProUGUI>().color = Color.green;

                        // on button click, set weapondata index to child index of button
                        attachmentButton.GetComponent<Button>().onClick.AddListener(() =>
                        {
                            foreach (var text in categoryContainer.GetComponentsInChildren<TextMeshProUGUI>()) text.color = Color.white;
                            attachmentButton.GetComponentInChildren<TextMeshProUGUI>().color = Color.green;

                            if(_menuType == MenuType.Vehicle)
                            {
                                _nv.CmdSetAttachmentCategoryIndex(
                                    categoryContainer.transform.GetSiblingIndex(),
                                    attachmentButton.transform.GetSiblingIndex() - (!category.UseFirstAsDefault ? 1 : 0)
                                );
                            }
                            else
                            {
                                _lm.CmdSetAttachmentCategoryIndex(
                                    _lm.ActiveLoadoutSlot,
                                    categoryContainer.transform.GetSiblingIndex(),
                                    attachmentButton.transform.GetSiblingIndex() - (!category.UseFirstAsDefault ? 1 : 0)
                                );
                            }
                        });
                    }

                    LayoutRebuilder.ForceRebuildLayoutImmediate(categoryContainer.GetComponent<RectTransform>());
                }

                gameObject.TweenCanvasGroupAlpha(1f, 0.2f)
                    .SetEase(EaseType.CubicInOut);
            }
            else // close
            {
                IsOpenSelf = false;
                Cursor.lockState = CursorLockMode.Locked;

                gameObject.TweenCanvasGroupAlpha(0f, 0.2f)
                    .SetEase(EaseType.CubicInOut)
                    .SetOnComplete(() => gameObject.SetActive(false))
                    .SetOnCancel(() => gameObject.SetActive(false));
            }
        }
    }
}
