using System.Linq;
using ElRaccoone.Tweens;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using Weapons;

namespace Player
{
    public enum LoadoutSlot
    {
        Primary,
        Secondary
    }

    public class PlayerLoadoutManager : NetworkBehaviour
    {
        public PlayerController PlayerController;

        public Weapon[] Weapons = new Weapon[2];

        public readonly SyncList<WeaponData> WeaponData = new() {null, null};

        private bool _weaponShown = true;

        public bool WeaponShown
        {
            get => _weaponShown;
            set
            {
                _weaponShown = value;

                if(Unarmed) return;
                PlayerController.WeaponRigContainer.gameObject.SetActive(value);
                EquipWeapon(value == false ? null : CurrentWeapon);
            }
        }

        public void ForceSyncData(LoadoutSlot slot)
        {
            var i = (int) slot;

            // force sync list because mirror is dumb
            var data = WeaponData[i];
            WeaponData.RemoveAt(i);
            WeaponData.Insert(i, data);
        }

        [SyncVar(hook = nameof(OnActiveLoadoutSlotChanged))]
        public LoadoutSlot ActiveLoadoutSlot;

        public Weapon CurrentWeapon => Weapons[(int) ActiveLoadoutSlot];

        public bool Unarmed => CurrentWeapon == null;

        public int CurrentAmmo
        {
            get => WeaponData[(int) ActiveLoadoutSlot].CurrentAmmo;
            set
            {
                var i = (int) ActiveLoadoutSlot;
                WeaponData[i].CurrentAmmo = value;

                ForceSyncData(ActiveLoadoutSlot);
            }
        }

        [Command]
        public void SetActiveLoadoutSlot(LoadoutSlot slot) => ActiveLoadoutSlot = slot;

        public void OnActiveLoadoutSlotChanged(LoadoutSlot oldSlot, LoadoutSlot newSlot)
        {
            if(Weapons[(int) oldSlot] != null) Weapons[(int) oldSlot].gameObject.SetActive(false);
            if(Weapons[(int) newSlot] != null) Weapons[(int) newSlot].gameObject.SetActive(true);

            // hacky way of detecting if we have a current weapon but actively are NOT equipping it
            // for example, if we're in a car, or jumping; a weapon is equipped but not in our hands
            // if so, keep it that way
            var activeButNotEquipped = !Unarmed
                                       && PlayerController.LeftHandConstraint.data.target != Weapons[(int) oldSlot].LeftHandIKTargetDown
                                       && PlayerController.LeftHandConstraint.data.target != Weapons[(int) oldSlot].LeftHandIKTargetAim;
            
            if(!activeButNotEquipped) EquipWeapon(Weapons[(int) newSlot]);
        }

        private void OnWeaponDataChanged(SyncList<WeaponData>.Operation op, int index, WeaponData oldWeaponData, WeaponData newWeaponData)
        {
            // gets re-instantiated when changed so we need to re-bind it to weapon
            if(newWeaponData != null)
            {
                Weapons[index].ApplySkin(newWeaponData.SkinIndex);
                Weapons[index].ApplyAttachments(newWeaponData.AttachmentIndexes);

                Weapons[index].WeaponData = newWeaponData;
            }

            // everything else is handled elsewhere in code
        }

        public void EquipWeapon(Weapon weapon)
        {
            // Enable ammo UI if we have a weapon
            if(PlayerController.isLocalPlayer) PlayerController.UIComponents.AmmoCounter.gameObject.SetActive(weapon != null);

            if(weapon != null)
            {
                PlayerController.LeftHandConstraint.data.target = weapon.LeftHandIKTargetDown;
                PlayerController.RightHandConstraint.data.target = weapon.RightHandIKTargetDown;

                if(PlayerController.CurrentState is PlayerStateAiming)
                {
                    PlayerController.WeaponRigContainer.localPosition = weapon.Positions.aimPosition;
                }
                else
                {
                    PlayerController.WeaponRigContainer.TweenCancelAll();
                    PlayerController.WeaponRigContainer.localPosition = weapon.Positions.downPosition;
                    PlayerController.WeaponRigContainer.localRotation = Quaternion.Euler(weapon.Positions.downRotation);
                }
            }
            else
            {
                PlayerController.LeftHandConstraint.data.target = null;
                PlayerController.RightHandConstraint.data.target = null;

                PlayerController.WeaponRigContainer.localPosition = Vector3.zero;
                PlayerController.WeaponRigContainer.localRotation = Quaternion.identity;
            }

            PlayerController.RebuildAnimator();
        }

        /// <summary>
        /// Set index of attachment to use in a given category
        /// </summary>
        /// <param name="slot">Slot of weapondata to modify</param>
        /// <param name="categoryIndex">Index of category to set attachment index of</param>
        /// <param name="attachmentIndex">Index of new attachment in category</param>
        [Command]
        public void CmdSetAttachmentCategoryIndex(LoadoutSlot slot, int categoryIndex, int attachmentIndex)
        {
            if(Weapons[(int) slot] == null) return;

            // if category index is more than number of categories, return
            if(categoryIndex >= Weapons[(int) slot].AttachmentCategories.Count) return;

            // if category index is higher than length of attachment indexes, fill with -1 to match length
            if(categoryIndex >= WeaponData[(int) slot].AttachmentIndexes.Length)
            {
                WeaponData[(int) slot].AttachmentIndexes = WeaponData[(int) slot].AttachmentIndexes
                    .Concat(Enumerable.Repeat(-1, categoryIndex - WeaponData[(int) slot].AttachmentIndexes.Length + 1))
                    .ToArray();
            }

            // if attachment index is more than number of attachments in category, return
            if(attachmentIndex >= Weapons[(int) slot].AttachmentCategories[categoryIndex].Attachments.Count) return;

            WeaponData[(int) slot].AttachmentIndexes[categoryIndex] = attachmentIndex;

            ForceSyncData(slot);
        }

        [Command]
        public void CmdSetSkinIndex(LoadoutSlot slot, int skinIndex)
        {
            if(skinIndex >= WeaponSkins.Skins.Count || skinIndex < 0) return;
            WeaponData[(int) slot].SkinIndex = skinIndex;

            ForceSyncData(slot);
        }

        [ClientRpc]
        public void RpcInstantReloadAll() => PlayerStateReloading.InstantReloadAll(PlayerController);

        public override void OnStartClient()
        {
            base.OnStartClient();

            WeaponData.Callback += OnWeaponDataChanged;

            // sync initial weapons on load
            for (var i = 0; i < WeaponData.Count; i++)
            {
                if(WeaponData[i] == null) continue;
                Weapons[i] = WeaponData[i].InstantiateFromData(PlayerController.WeaponRigContainer);
            }

            // sync initial equipped weapon
            OnActiveLoadoutSlotChanged(LoadoutSlot.Primary, ActiveLoadoutSlot);
            EquipWeapon(CurrentWeapon);

            if(!isLocalPlayer) return;
            PlayerController.PlayerInput.actions["EquipPrimary"].performed += OnEquipPrimary;
            PlayerController.PlayerInput.actions["EquipSecondary"].performed += OnEquipSecondary;

            PlayerController.PlayerInput.actions["ToggleEquip"].performed += OnToggleEquip;
        }

        private void OnDisable()
        {
            if(!isLocalPlayer) return;
            PlayerController.PlayerInput.actions["EquipPrimary"].performed -= OnEquipPrimary;
            PlayerController.PlayerInput.actions["EquipSecondary"].performed -= OnEquipSecondary;

            PlayerController.PlayerInput.actions["ToggleEquip"].performed -= OnToggleEquip;
        }

        private void OnEquipPrimary(InputAction.CallbackContext ctx)
        {
            if(PlayerController.DisableInput) return;

            if(PlayerController.CurrentState is PlayerStateReloading) SharedStateFunctionality.TransferToAppropriateState(PlayerController);
            SetActiveLoadoutSlot(LoadoutSlot.Primary);
        }

        private void OnEquipSecondary(InputAction.CallbackContext ctx)
        {
            if(PlayerController.DisableInput) return;

            if(PlayerController.CurrentState is PlayerStateReloading) SharedStateFunctionality.TransferToAppropriateState(PlayerController);
            SetActiveLoadoutSlot(LoadoutSlot.Secondary);
        }

        private void OnToggleEquip(InputAction.CallbackContext ctx)
        {
            if(PlayerController.DisableInput) return;

            if(ctx.performed)
            {
                if(PlayerController.CurrentState is PlayerStateReloading) SharedStateFunctionality.TransferToAppropriateState(PlayerController);
                SetActiveLoadoutSlot(ActiveLoadoutSlot == LoadoutSlot.Primary ? LoadoutSlot.Secondary : LoadoutSlot.Primary);
            }
        }

        private void Update()
        {
            if(isLocalPlayer && WeaponData[(int) ActiveLoadoutSlot] != null)
            {
                PlayerController.UIComponents.AmmoCounter.text = $"<b>{CurrentAmmo}</b> <color=#DDDDDD>{CurrentWeapon.MaxAmmo}</color>";
            }
        }
    }
}
