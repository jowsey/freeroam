using System;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using Core;
using ElRaccoone.Tweens;
using ElRaccoone.Tweens.Core;
using Mirror;
using UI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;
using Vehicles;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Player
{
    public static class SharedStateFunctionality
    {
        public static void ProcessMovement(PlayerController playerController)
        {
            var cameraTransform = playerController.CameraComponents.Camera.transform;

            var moveDirRelativeToCam = cameraTransform.TransformDirection(playerController.MoveInputDir).Flatten().normalized;
            var moveDir = moveDirRelativeToCam * (playerController.BaseMovementSpeed * playerController.MovementSpeedModifier);

            if(!playerController.Rigidbody.isKinematic) playerController.Rigidbody.velocity = moveDir + Vector3.up * playerController.Rigidbody.velocity.y;
        }

        /// <summary>
        /// Auto-select a new movement state based on current inputs
        /// </summary>
        [Client]
        public static void TransferToAppropriateState(PlayerController playerController)
        {
            if(!playerController.LoadoutManager.Unarmed && playerController.CurrentState is not PlayerStateAiming)
            {
                // aiming
                if(playerController.PlayerInput.actions["Aim"].IsPressed())
                {
                    playerController.NetworkSetHipFiring(false);
                    playerController.UpdateState(playerController.AimingState);
                    return;
                }

                // hip-firing
                if(playerController.PlayerInput.actions["Fire"].IsPressed())
                {
                    playerController.NetworkSetHipFiring(true);
                    playerController.UpdateState(playerController.AimingState);
                    return;
                }
            }

            // sprinting
            if(playerController.SprintAction.IsPressed())
            {
                playerController.UpdateState(playerController.SprintingState);
            }
            // walking or idle
            else
            {
                playerController.UpdateState(playerController.MoveInputDir != Vector3.zero ? playerController.WalkingState : playerController.IdleState);
            }
        }

        public static void SetAllRigsDamping(IEnumerable<CinemachineOrbitalTransposer> rigs, float damping)
        {
            foreach (var rig in rigs) rig.m_XDamping = rig.m_YDamping = rig.m_ZDamping = damping;
        }
    }

    [Serializable]
    public abstract class PlayerState
    {
        public virtual void OnEnter(PlayerController playerController, PlayerState oldState)
        {
        }

        public virtual void OnExit(PlayerController playerController)
        {
        }

        public virtual void OnUpdate(PlayerController playerController)
        {
            if(!playerController.isLocalPlayer) return;

            var moveDir = playerController.MovementAction.ReadValue<Vector2>();
            playerController.MoveInputDir = PlayerController.DisableInput ? Vector3.zero : new Vector3(moveDir.x, 0, moveDir.y);
        }

        public virtual void OnFixedUpdate(PlayerController playerController)
        {
            if(!playerController.isLocalPlayer) return;

            // Grounded check
            if(!playerController.IsGrounded)
            {
                if(Time.time - playerController.LastGroundedTime > 0.1f && playerController.Rigidbody.velocity.y < 0)
                {
                    playerController.UpdateState(playerController.FallingState);
                }
            }
            else
            {
                playerController.LastGroundedTime = Time.time;

                if(playerController.Animator.GetBool(PlayerController.FallingHash) && playerController.Rigidbody.velocity.y >= 0 &&
                   playerController.LastJumpTime - Time.time > 0.2f)
                {
                    playerController.Animator.SetBool(PlayerController.FallingHash, false);
                }
            }

            // Swimming check
            if(playerController.IsSubmerged && playerController.CurrentState is not PlayerStateSwimming) playerController.UpdateState(playerController.SwimmingState);

            // Animation
            var animator = playerController.Animator;
            var currentParams = new Vector3(animator.GetFloat(PlayerController.MoveSidewaysHash), 0, animator.GetFloat(PlayerController.MoveForwardsHash));
            if(playerController.CurrentState is PlayerStateAiming) currentParams /= 1.8f;

            var velocity = playerController.Rigidbody.position - playerController.LastRbPos;
            var velocityOverTime = velocity / Time.fixedDeltaTime;
            var velocityRelativeToMax = velocityOverTime / playerController.BaseMovementSpeed * playerController.MovementSpeedModifier;

            var localVelocity = playerController.transform.InverseTransformDirection(velocityRelativeToMax);
            var lerped = Vector3.Lerp(currentParams, localVelocity, Time.deltaTime * 6f);

            if(playerController.CurrentState is PlayerStateAiming) lerped *= 1.8f;

            lerped = Vector3.ClampMagnitude(lerped, 1f);

            animator.SetFloat(PlayerController.MoveForwardsHash, lerped.z);
            animator.SetFloat(PlayerController.MoveSidewaysHash, lerped.x);

            playerController.LastRbPos = playerController.Rigidbody.position;
        }

        public virtual void OnLateUpdate(PlayerController playerController)
        {
        }

        public virtual void OnMove(PlayerController playerController, InputAction.CallbackContext context)
        {
        }

        public virtual void OnLook(PlayerController playerController, InputAction.CallbackContext context)
        {
        }

        public virtual void OnAim(PlayerController playerController, InputAction.CallbackContext context)
        {
            if(!playerController.isLocalPlayer || PlayerController.DisableInput || playerController.LoadoutManager.Unarmed || !context.action.WasPressedThisFrame()) return;

            playerController.NetworkSetHipFiring(false);
            playerController.UpdateState(playerController.AimingState);
        }

        public virtual void OnFire(PlayerController playerController, InputAction.CallbackContext context)
        {
            if(!playerController.isLocalPlayer || PlayerController.DisableInput || playerController.LoadoutManager.Unarmed || !context.action.WasPressedThisFrame()) return;

            playerController.NetworkSetHipFiring(true);
            playerController.UpdateState(playerController.AimingState);
        }

        public virtual void OnSprint(PlayerController playerController, InputAction.CallbackContext context)
        {
        }

        public virtual void OnVault(PlayerController playerController, InputAction.CallbackContext context)
        {
            if(!playerController.isLocalPlayer || PlayerController.DisableInput) return;
            var target = Vector3.zero;
            var normal = Vector3.zero;

            if(context.action.WasPressedThisFrame())
            {
                const float maxRelativeLedgeHeight = 4f;

                var foundLedge = false;
                var iteration = 1;

                while (!foundLedge)
                {
                    var ray = new Ray(playerController.transform.position + Vector3.up * (0.5f * iteration++), playerController.transform.forward);
                    if(ray.origin.y > playerController.transform.position.y + maxRelativeLedgeHeight) break;

                    if(Physics.Raycast(ray, out var hit, 1f, ~LayerMask.GetMask("Player", "Vehicle")))
                    {
                        target = hit.point + Vector3.up * 0.25f;
                        normal += hit.normal;
                    }
                    else if(target != Vector3.zero)
                    {
                        foundLedge = true;
                    }
                }

                if(foundLedge)
                {
                    playerController.CmdVault(target, normal);
                }
                else if(Time.time - playerController.LastJumpTime > playerController.JumpCooldown && playerController.IsGrounded)
                {
                    if(playerController.CurrentState is PlayerStateSwimming) return;

                    playerController.Rigidbody.AddForce(
                        (playerController.transform.up + playerController.transform.forward * 0.7f) * playerController.JumpForce,
                        ForceMode.Impulse
                    );

                    playerController.LastJumpTime = Time.time;

                    playerController.Animator.SetBool(PlayerController.FallingHash, true);
                    playerController.UpdateState(playerController.FallingState);
                }
            }
        }

        public virtual void OnReload(PlayerController playerController, InputAction.CallbackContext context)
        {
            if(!playerController.isLocalPlayer || PlayerController.DisableInput) return;

            var lm = playerController.LoadoutManager;
            if(context.action.WasPerformedThisFrame() && context.interaction is TapInteraction && !lm.Unarmed && lm.CurrentAmmo < lm.CurrentWeapon.MaxAmmo)
            {
                playerController.UpdateState(playerController.ReloadingState);
            }
        }

        public virtual void OnEnterVehicle(PlayerController playerController, InputAction.CallbackContext context)
        {
            if(!playerController.isLocalPlayer || PlayerController.DisableInput) return;

            if(context.action.WasPerformedThisFrame())
            {
                // get closest vehicle seat
                var closest = Object.FindObjectsByType<VehicleSeat>(FindObjectsSortMode.None)
                    .Where(v => Vector3.Distance(v.transform.position, playerController.transform.position) <= 5f)
                    .OrderBy(v => Vector3.Distance(v.transform.position, playerController.transform.position))
                    .FirstOrDefault();

                if(closest != null) closest.GetComponentInParent<NetworkVehicle>().CmdEnter();
            }
        }
    }

    public class PlayerStateIdle : PlayerState
    {
        public override void OnUpdate(PlayerController playerController)
        {
            base.OnUpdate(playerController);

            if(!playerController.isLocalPlayer) return;

            // Begin walking when input begins
            if(playerController.MoveInputDir != Vector3.zero)
                playerController.UpdateState(playerController.WalkingState);
        }
    }

    public class PlayerStateWalking : PlayerState
    {
        public override void OnEnter(PlayerController playerController, PlayerState oldState)
        {
            if(!playerController.isLocalPlayer) return;

            playerController.MovementSpeedModifier = 1f;

            if(playerController.SprintAction.IsPressed())
                playerController.UpdateState(playerController.SprintingState);
        }

        public override void OnUpdate(PlayerController playerController)
        {
            base.OnUpdate(playerController);

            if(!playerController.isLocalPlayer) return;

            // Go idle when input stops
            if(playerController.MoveInputDir == Vector3.zero)
                playerController.UpdateState(playerController.IdleState);
        }

        public override void OnFixedUpdate(PlayerController playerController)
        {
            if(!playerController.isLocalPlayer) return;

            SharedStateFunctionality.ProcessMovement(playerController);

            // Rotate to face movement direction
            if(playerController.MoveInputDir != Vector3.zero)
            {
                var moveDirRelativeToCam = playerController.CameraComponents.Camera.transform.TransformDirection(playerController.MoveInputDir).Flatten().normalized;

                playerController.Rigidbody.MoveRotation(
                    Quaternion.Lerp(
                        playerController.Rigidbody.rotation,
                        Quaternion.LookRotation(moveDirRelativeToCam),
                        Time.fixedDeltaTime * 5f
                    )
                );
            }

            base.OnFixedUpdate(playerController);
        }

        public override void OnSprint(PlayerController playerController, InputAction.CallbackContext context)
        {
            if(!playerController.isLocalPlayer || PlayerController.DisableInput) return;

            if(context.action.WasPressedThisFrame())
            {
                playerController.UpdateState(playerController.SprintingState);
            }
        }
    }

    public class PlayerStateSprinting : PlayerState
    {
        public override void OnEnter(PlayerController playerController, PlayerState oldState)
        {
            if(!playerController.isLocalPlayer) return;

            playerController.MovementSpeedModifier = playerController.SprintingSpeedModifier;
            playerController.Animator.SetBool(PlayerController.SprintingHash, true);
        }

        public override void OnUpdate(PlayerController playerController)
        {
            base.OnUpdate(playerController);

            if(!playerController.isLocalPlayer) return;

            // Go idle when input stops
            if(playerController.MoveInputDir == Vector3.zero)
                playerController.UpdateState(playerController.IdleState);
        }

        public override void OnFixedUpdate(PlayerController playerController)
        {
            // Inherit state from walking
            playerController.WalkingState.OnFixedUpdate(playerController);
        }

        public override void OnSprint(PlayerController playerController, InputAction.CallbackContext context)
        {
            if(!playerController.isLocalPlayer) return;

            var controlScheme = playerController.PlayerInput.currentControlScheme;

            switch (controlScheme)
            {
                // Hold sprint on KB+M, toggle sprint on gamepad
                case "KB+M" when context.action.WasReleasedThisFrame():
                case "Gamepad" when context.action.WasPressedThisFrame():
                    playerController.UpdateState(playerController.WalkingState);
                    break;
            }
        }

        public override void OnExit(PlayerController playerController)
        {
            if(!playerController.isLocalPlayer) return;

            playerController.Animator.SetBool(PlayerController.SprintingHash, false);
        }
    }

    public class PlayerStateAiming : PlayerState
    {
        /// <summary>
        /// Disallow aiming for a moment after entering aim state to give time for weapon to be raised
        /// </summary>
        private float _enterAimStateTime;

        private const float _aimRightOffset = 0.4f;
        private const float _aimForwardOffset = 2.5f;
        private float _modifiedForwardOffset = _aimForwardOffset;

        private Tween<Vector3> _weaponRotTween;
        private Tween<Vector3> _cameraZoomTween;

        public bool IsHipFiring;

        public override void OnEnter(PlayerController playerController, PlayerState oldState)
        {
            if(playerController.isLocalPlayer)
            {
                UpdateForwardOffset(playerController);
                SetAimTarget(playerController);

                _enterAimStateTime = Time.time;

                if(!IsHipFiring)
                {
                    // Move camera to over-the-shoulder
                    var co = playerController.CameraComponents.CameraOffset;
                    _cameraZoomTween = co.TweenValueVector3(Vector3.right * _aimRightOffset + Vector3.forward * _modifiedForwardOffset, 0.25f, val => co.m_Offset = val)
                        .SetEase(EaseType.QuadOut)
                        .SetFrom(Vector3.zero);

                    // Set move speed
                    playerController.MovementSpeedModifier = playerController.AimingSpeedModifier;

                    // Show crosshair
                    playerController.UIComponents.Crosshair.gameObject.SetActive(true);

                    // Set sensitivity
                    playerController.CameraComponents.FreeLook.m_XAxis.m_MaxSpeed *= playerController.CameraAimSensModifier;

                    // Set camera damping to 0
                    SharedStateFunctionality.SetAllRigsDamping(playerController.CameraComponents.FreeLook.GetAllRigs(), 0f);
                }
                else
                {
                    playerController.MovementSpeedModifier = 1f;
                }
            }

            // Enable animation rigging
            playerController.BodyTrackingLayer.active = true;
            playerController.GunPointingLayer.active = true;

            // Move weapon to aiming position
            playerController.WeaponRigContainer.TweenLocalPosition(playerController.LoadoutManager.CurrentWeapon.Positions.aimPosition, 0.5f)
                .SetEase(EaseType.QuadOut);

            if(_weaponRotTween != null) _weaponRotTween.Cancel();

            // Set hand IK targets
            playerController.LeftHandConstraint.data.target = playerController.LoadoutManager.CurrentWeapon.LeftHandIKTargetAim;
            playerController.RightHandConstraint.data.target = playerController.LoadoutManager.CurrentWeapon.RightHandIKTargetAim;
            playerController.RebuildAnimator();

            playerController.Animator.SetBool(PlayerController.AimingHash, true);
        }

        public override void OnExit(PlayerController playerController)
        {
            if(playerController.isLocalPlayer)
            {
                if(!IsHipFiring)
                {
                    // Move camera back to original position
                    var co = playerController.CameraComponents.CameraOffset;
                    _cameraZoomTween.Cancel();
                    _cameraZoomTween = co.TweenValueVector3(Vector3.zero, 0.25f, val => co.m_Offset = val)
                        .SetEase(EaseType.QuintOut)
                        .SetFrom(co.m_Offset);

                    // Hide crosshair
                    playerController.UIComponents.Crosshair.gameObject.SetActive(false);

                    // Set sensitivity
                    playerController.CameraComponents.FreeLook.m_XAxis.m_MaxSpeed /= playerController.CameraAimSensModifier;

                    // Set camera damping back#
                    SharedStateFunctionality.SetAllRigsDamping(playerController.CameraComponents.FreeLook.GetAllRigs(), 0.1f);
                }
                else
                {
                    playerController.NetworkSetHipFiring(false);
                }
            }

            // Disable animation rigging
            playerController.BodyTrackingLayer.active = false;
            playerController.GunPointingLayer.active = false;

            // Move weapon to down position
            playerController.WeaponRigContainer.TweenLocalPosition(playerController.LoadoutManager.CurrentWeapon.Positions.downPosition, 0.5f)
                .SetEase(EaseType.CubicOut);

            _weaponRotTween = playerController.WeaponRigContainer.TweenLocalRotation(playerController.LoadoutManager.CurrentWeapon.Positions.downRotation, 0.5f)
                .SetEase(EaseType.CubicOut);

            // Set hand IK targets
            playerController.LeftHandConstraint.data.target = playerController.LoadoutManager.CurrentWeapon.LeftHandIKTargetDown;
            playerController.RightHandConstraint.data.target = playerController.LoadoutManager.CurrentWeapon.RightHandIKTargetDown;
            playerController.RebuildAnimator();

            playerController.Animator.SetBool(PlayerController.AimingHash, false);
        }

        /// <summary>   
        /// Moves camera forward when looking up for better view
        /// </summary>
        private void UpdateForwardOffset(PlayerController playerController)
        {
            if(IsHipFiring || _cameraZoomTween) return;

            // Lerp offset z to 0 when looking up (from camera x rot 0 to -45)
            var cameraXRot = playerController.CameraComponents.Camera.transform.localEulerAngles.x;
            if(cameraXRot < 315f) return;

            _modifiedForwardOffset = Mathf.Lerp(_aimForwardOffset, 0f, Mathf.InverseLerp(360f, 315f, cameraXRot));

            playerController.CameraComponents.CameraOffset.m_Offset = Vector3.Lerp(
                playerController.CameraComponents.CameraOffset.m_Offset,
                Vector3.right * _aimRightOffset + Vector3.forward * _modifiedForwardOffset,
                Time.deltaTime * 10f
            );
        }

        public override void OnUpdate(PlayerController playerController)
        {
            base.OnUpdate(playerController);

            if(!playerController.isLocalPlayer) return;
            if(PlayerController.DisableInput) SharedStateFunctionality.TransferToAppropriateState(playerController);

            SetAimTarget(playerController);
            UpdateForwardOffset(playerController);

            // Weapon firing
            var lm = playerController.LoadoutManager;
            if(!lm.Unarmed)
            {
                var isReadyToFire = NetworkTime.time - lm.CurrentWeapon.LastFiredTimestamp > lm.CurrentWeapon.TimeBetweenShots;
                var isEnterAimStateDelayFinished = Time.time - _enterAimStateTime > (IsHipFiring ? 0.1f : 0.25f);

                if(playerController.FireAction.IsPressed() && isReadyToFire && isEnterAimStateDelayFinished)
                {
                    if(lm.CurrentAmmo > 0 && !playerController.LoadoutManager.CurrentWeapon.ReloadStateData.IsMidReload)
                    {
                        playerController.NetworkFire();

                        // Tween camera recoil
                        var fl = playerController.CameraComponents.FreeLook;

                        var lastVal = Vector2.zero;
                        var recoil = -lm.CurrentWeapon.ModifiedRecoil * new Vector2(Random.Range(-1f, 1f), Random.Range(0.5f, 1f));

                        fl.TweenValueVector2(recoil, 0.075f, val =>
                        {
                            var diff = val - lastVal;
                            lastVal = val;

                            fl.m_YAxis.Value += diff.y;
                            fl.m_XAxis.Value += diff.x;
                        }).SetEase(EaseType.ExpoOut);
                    }
                    else
                    {
                        playerController.UpdateState(playerController.ReloadingState);
                    }
                }
            }
        }

        private void SetAimTarget(PlayerController playerController)
        {
            var pos = playerController.CameraComponents.Camera.ViewportPointToRay(Vector3.one * 0.5f).GetPoint(10f);
            var rot = playerController.CameraComponents.Camera.transform.rotation;

            var localPos = playerController.transform.InverseTransformPoint(pos);
            var localRot = Quaternion.Inverse(playerController.transform.rotation) * rot;
            
            playerController.AimTarget.localPosition = localPos;
            playerController.AimTarget.localRotation = localRot;
            playerController.CmdSetAimTarget(localPos, localRot);
        }

        public override void OnFixedUpdate(PlayerController playerController)
        {
            if(!playerController.isLocalPlayer) return;

            SharedStateFunctionality.ProcessMovement(playerController);

            // Rotate to face same direction as camera
            var cameraDir = playerController.CameraComponents.Camera.transform.forward.Flatten().normalized;

            playerController.Rigidbody.MoveRotation(Quaternion.LookRotation(cameraDir));

            base.OnFixedUpdate(playerController);
        }

        public override void OnAim(PlayerController playerController, InputAction.CallbackContext context)
        {
            if(!playerController.isLocalPlayer || IsHipFiring) return;

            if(context.action.WasReleasedThisFrame()) SharedStateFunctionality.TransferToAppropriateState(playerController);
        }

        public override void OnFire(PlayerController playerController, InputAction.CallbackContext context)
        {
            if(!playerController.isLocalPlayer || !IsHipFiring) return;

            if(context.action.WasReleasedThisFrame()) SharedStateFunctionality.TransferToAppropriateState(playerController);
        }
    }

    public class PlayerStateVaulting : PlayerState
    {
        public Vector3 LedgePosition;
        public Vector3 LedgeNormal;

        public override void OnEnter(PlayerController playerController, PlayerState oldState)
        {
            if(playerController.isLocalPlayer)
            {
                var playerRot = playerController.transform.localEulerAngles;
                var ledgeRotation = Quaternion.LookRotation(LedgeNormal) * Quaternion.Euler(0f, 180f, 0f);

                playerController.transform.localEulerAngles = new Vector3(playerRot.x, ledgeRotation.eulerAngles.y, playerRot.z);

                playerController.Rigidbody.isKinematic = true;
                playerController.Animator.SetBool(PlayerController.FallingHash, true);

                playerController.Rigidbody.excludeLayers |= LayerMask.GetMask("Vehicle", "Ignore Raycast");
            }

            playerController.LoadoutManager.WeaponShown = false;

            var leftPos = LedgePosition + playerController.transform.TransformDirection(Vector3.right * -0.5f);
            var rightPos = LedgePosition + playerController.transform.TransformDirection(Vector3.right * 0.5f);

            var leftTarget = new GameObject("LeftHandTarget");
            var rightTarget = new GameObject("RightHandTarget");

            leftTarget.transform.parent = playerController.WeaponRigContainer;
            rightTarget.transform.parent = playerController.WeaponRigContainer;

            playerController.LeftHandConstraint.data.target = leftTarget.transform;
            playerController.RightHandConstraint.data.target = rightTarget.transform;

            // angle targets so that hands face the right direction
            var leftTargetRot = Quaternion.LookRotation(playerController.transform.forward, playerController.transform.up);
            var rightTargetRot = Quaternion.LookRotation(-playerController.transform.forward, -playerController.transform.up);

            leftTargetRot *= Quaternion.Euler(0f, 90f, 0f);
            rightTargetRot *= Quaternion.Euler(0f, 90f, 0f);

            leftTarget.transform.rotation = leftTargetRot;
            rightTarget.transform.rotation = rightTargetRot;

            playerController.RebuildAnimator();

            void SharedFinish()
            {
                playerController.LoadoutManager.WeaponShown = true;

                Object.Destroy(leftTarget, 0.05f);
                Object.Destroy(rightTarget, 0.05f);
            }

            var duration = (LedgePosition.y - playerController.transform.position.y) * 0.2f;
            if(playerController.isLocalPlayer)
            {
                var startY = playerController.transform.position.y;
                playerController.TweenValueVector3(LedgePosition, duration, val =>
                    {
                        var progress = Mathf.InverseLerp(startY, LedgePosition.y, val.y);

                        // rotate hands from 0 to 90 degrees on the x from start to finish
                        var rot = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 90f, progress));
                        leftTarget.transform.rotation = leftTargetRot * rot;
                        rightTarget.transform.rotation = rightTargetRot * rot;

                        // playerController.transform.position = val;
                        playerController.Rigidbody.MovePosition(val);

                        leftTarget.transform.position = leftPos;
                        rightTarget.transform.position = rightPos;
                    })
                    .SetFrom(playerController.transform.position)
                    .SetEase(EaseType.SineOut)
                    .SetOnComplete(() =>
                    {
                        // todo maybe look into moving to OnExit
                        playerController.Rigidbody.isKinematic = false;
                        playerController.Rigidbody.AddForce(playerController.transform.forward * 3f, ForceMode.Impulse);
                        playerController.Rigidbody.excludeLayers &= ~LayerMask.GetMask("Vehicle", "Ignore Raycast");

                        SharedFinish();

                        playerController.Animator.SetBool(PlayerController.FallingHash, false);
                        SharedStateFunctionality.TransferToAppropriateState(playerController);
                    });
            }
            else
            {
                // this just functions as a delay
                playerController.TweenValueFloat(0f, duration, _ =>
                    {
                        leftTarget.transform.position = leftPos;
                        rightTarget.transform.position = rightPos;
                    })
                    .SetOnComplete(SharedFinish);
            }
        }

        public override void OnMove(PlayerController playerController, InputAction.CallbackContext context)
        {
        }

        public override void OnVault(PlayerController playerController, InputAction.CallbackContext context)
        {
        }

        public override void OnAim(PlayerController playerController, InputAction.CallbackContext context)
        {
        }
    }

    public class PlayerStateFalling : PlayerState
    {
        private float _originalDrag;

        public override void OnEnter(PlayerController playerController, PlayerState oldState)
        {
            if(!playerController.isLocalPlayer) return;

            _originalDrag = playerController.Rigidbody.drag;

            playerController.Animator.SetBool(PlayerController.FallingHash, true);
            playerController.Rigidbody.drag = _originalDrag / 10f;
        }

        public override void OnFixedUpdate(PlayerController playerController)
        {
            if(!playerController.isLocalPlayer) return;

            // playerController.WalkingState.OnFixedUpdate(playerController);

            if(playerController.IsGrounded && Time.time - playerController.LastJumpTime > 0.2f)
            {
                SharedStateFunctionality.TransferToAppropriateState(playerController);
            }
        }

        public override void OnAim(PlayerController playerController, InputAction.CallbackContext context)
        {
        }

        public override void OnExit(PlayerController playerController)
        {
            if(!playerController.isLocalPlayer) return;

            playerController.Animator.SetBool(PlayerController.FallingHash, false);
            playerController.Rigidbody.drag = _originalDrag;
        }

        public override void OnReload(PlayerController playerController, InputAction.CallbackContext context)
        {
        }
    }

    public class PlayerStateReloading : PlayerState
    {
        private GameObject _leftHandFollowTarget;

        public static void InstantReloadAll(PlayerController playerController)
        {
            var lm = playerController.LoadoutManager;

            var weapons = lm.Weapons.Where(w => w != null).ToList();
            for (var i = 0; i < weapons.Count; i++)
            {
                var wep = weapons[i];

                if(wep.ReloadStateData.NewMagazine)
                {
                    wep.ReloadStateData.NewMagazine.SetActive(true);
                    wep.ReloadStateData.NewMagazine.transform.localPosition = wep.MagazineLocalPos;

                    wep.ReloadStateData.NewMagazine = null;
                    wep.ReloadStateData.IsMidReload = false;
                }

                if(playerController.isServer)
                {
                    wep.WeaponData.CurrentAmmo = wep.MaxAmmo;
                    lm.ForceSyncData((LoadoutSlot) i);
                }
            }
        }

        public override void OnEnter(PlayerController playerController, PlayerState oldState)
        {
            playerController.GunPointingLayer.active = true;

            var curWep = playerController.LoadoutManager.CurrentWeapon;
            var wepReloadData = curWep.ReloadStateData;

            var IsContinuingPreviousReload = wepReloadData.IsMidReload;
            wepReloadData.IsMidReload = true;

            var reloadTime = curWep.ModifiedReloadTime;

            if(playerController.isLocalPlayer)
            {
                // force set aimtarget to be at the same height as the player to make gun point flat(ish)
                playerController.AimTarget.localPosition = new Vector3(playerController.AimTarget.localPosition.x, 2f, playerController.AimTarget.localPosition.z);
                playerController.CmdSetAimTarget(playerController.AimTarget.localPosition, playerController.AimTarget.localRotation);

                playerController.MovementSpeedModifier = 1f;
            }

            // Move weapon to up position
            playerController.WeaponRigContainer.TweenCancelAll(); // prevents conflict with aiming state
            playerController.WeaponRigContainer.TweenLocalPosition(curWep.Positions.aimPosition, reloadTime * 0.2f)
                .SetEase(EaseType.QuadOut)
                .SetOnComplete(() =>
                {
                    var oldMagazine = curWep.Magazine;

                    // Create new magazine
                    if(!wepReloadData.NewMagazine) wepReloadData.NewMagazine = Object.Instantiate(oldMagazine, oldMagazine.transform.parent);

                    var worldSpacePocketPos = playerController.transform.TransformPoint(new Vector3(-0.25f, 0.9f, 0f));
                    var pocketPosLocalFromWeapon = curWep.transform.InverseTransformPoint(worldSpacePocketPos);

                    wepReloadData.NewMagazine.transform.localPosition = pocketPosLocalFromWeapon;
                    wepReloadData.NewMagazine.transform.SetSiblingIndex(oldMagazine.transform.GetSiblingIndex());
                    wepReloadData.NewMagazine.SetActive(false);

                    curWep.Magazine = wepReloadData.NewMagazine;

                    // If there's a mag in the gun, drop it
                    if(!IsContinuingPreviousReload)
                    {
                        AudioSource.PlayClipAtPoint(Addressables.LoadAssetAsync<AudioClip>("Audio/MagUnpack").WaitForCompletion(), oldMagazine.transform.position, 0.2f);
                        oldMagazine.GetComponent<Rigidbody>().isKinematic = false;
                        oldMagazine.GetComponent<Collider>().enabled = true;
                        oldMagazine.transform.parent = null;

                        oldMagazine.TweenLocalScale(Vector3.zero, 1f)
                            .SetEase(EaseType.CubicInOut)
                            .SetDelay(10f)
                            .SetOnComplete(() => Object.Destroy(oldMagazine));
                    }

                    // Set left hand to follow target
                    _leftHandFollowTarget = new GameObject("LeftHandTarget")
                    {
                        transform =
                        {
                            parent = curWep.transform,
                            localPosition = Vector3.zero,
                            localEulerAngles = new Vector3(-90f, 90f, 0f)
                        }
                    };

                    playerController.LeftHandConstraint.data.target = _leftHandFollowTarget.transform;
                    playerController.RebuildAnimator();

                    var handPosOffset = new Vector3(-0.08f, 0f, -0.12f);

                    // Move left hand to pocket
                    _leftHandFollowTarget.TweenLocalPosition(pocketPosLocalFromWeapon + handPosOffset, reloadTime * 0.25f)
                        .SetEase(EaseType.CubicInOut)
                        .SetOnComplete(() =>
                        {
                            wepReloadData.NewMagazine.SetActive(true);
                            wepReloadData.NewMagazine.TweenLocalScale(Vector3.one, 0.2f)
                                .SetFrom(Vector3.zero)
                                .SetEase(EaseType.CubicInOut);

                            var magLocalPosLow = curWep.MagazineLocalPos - wepReloadData.NewMagazine.transform.up * 0.1f;

                            wepReloadData.NewMagazine.TweenLocalPosition(magLocalPosLow, reloadTime * 0.3f)
                                .SetEase(EaseType.CubicIn);

                            // Move left hand + mag back to original position
                            _leftHandFollowTarget.TweenLocalPosition(magLocalPosLow + handPosOffset, reloadTime * 0.3f)
                                .SetEase(EaseType.CubicIn)
                                .SetOnComplete(() =>
                                {
                                    AudioSource.PlayClipAtPoint(Addressables.LoadAssetAsync<AudioClip>("Audio/MagPack").WaitForCompletion(), curWep.transform.position, 0.2f);

                                    wepReloadData.NewMagazine.TweenLocalPosition(curWep.MagazineLocalPos, reloadTime * 0.25f)
                                        .SetEase(EaseType.CubicOut);

                                    _leftHandFollowTarget.TweenLocalPosition(curWep.MagazineLocalPos + handPosOffset, reloadTime * 0.25f)
                                        .SetEase(EaseType.CubicOut)
                                        .SetOnComplete(() =>
                                        {
                                            playerController.LeftHandConstraint.data.target = curWep.LeftHandIKTargetDown;
                                            playerController.RebuildAnimator();

                                            Object.Destroy(_leftHandFollowTarget, 0.1f);

                                            wepReloadData.NewMagazine = null;
                                            wepReloadData.IsMidReload = false;

                                            if(playerController.isServer)
                                            {
                                                playerController.LoadoutManager.CurrentAmmo = curWep.MaxAmmo;
                                                playerController.TargetTransferToAppropriateState();
                                            }
                                        });
                                });
                        });
                });
        }

        public override void OnExit(PlayerController playerController)
        {
            playerController.GunPointingLayer.active = false;

            playerController.TweenCancelAll();

            playerController.LeftHandConstraint.data.target = playerController.LoadoutManager.CurrentWeapon.LeftHandIKTargetDown;
            playerController.RebuildAnimator();

            if(_leftHandFollowTarget)
            {
                _leftHandFollowTarget.TweenCancelAll();
                Object.Destroy(_leftHandFollowTarget, 0.1f);
            }

            var reloadStateData = playerController.LoadoutManager.CurrentWeapon.ReloadStateData;
            if(reloadStateData.NewMagazine)
            {
                reloadStateData.NewMagazine.TweenCancelAll();
                reloadStateData.NewMagazine.SetActive(false);
            }

            // Move weapon to down position
            playerController.WeaponRigContainer.TweenLocalPosition(playerController.LoadoutManager.CurrentWeapon.Positions.downPosition, 0.5f)
                .SetEase(EaseType.CubicInOut);

            playerController.WeaponRigContainer.TweenLocalRotation(playerController.LoadoutManager.CurrentWeapon.Positions.downRotation, 0.5f)
                .SetEase(EaseType.CubicInOut);
        }

        public override void OnFixedUpdate(PlayerController playerController)
        {
            // Inherit state from walking
            playerController.WalkingState.OnFixedUpdate(playerController);
        }

        public override void OnFire(PlayerController playerController, InputAction.CallbackContext context)
        {
        }

        public override void OnAim(PlayerController playerController, InputAction.CallbackContext context)
        {
        }

        public override void OnReload(PlayerController playerController, InputAction.CallbackContext context)
        {
        }
    }

    public class PlayerStateSwimming : PlayerState
    {
        public override void OnEnter(PlayerController playerController, PlayerState oldState)
        {
            playerController.Animator.SetBool(PlayerController.SwimmingHash, true);

            playerController.LoadoutManager.WeaponShown = false;

            playerController.IK.enabled = false;

            if(playerController.isLocalPlayer)
            {
                playerController.Rigidbody.useGravity = false;
                playerController.MovementSpeedModifier = 1f;
                
                playerController.Rigidbody.rotation = Quaternion.Euler(0f, playerController.Rigidbody.rotation.eulerAngles.y, 0f);
                Physics.SyncTransforms();
            }
        }

        public override void OnFixedUpdate(PlayerController playerController)
        {
            if(!playerController.IsSubmerged && playerController.isLocalPlayer)
            {
                SharedStateFunctionality.TransferToAppropriateState(playerController);
                return;
            }

            playerController.WalkingState.OnFixedUpdate(playerController);
        }

        public override void OnExit(PlayerController playerController)
        {
            playerController.Animator.SetBool(PlayerController.SwimmingHash, false);

            playerController.LoadoutManager.WeaponShown = true;

            playerController.IK.enabled = true;

            if(playerController.isLocalPlayer) playerController.Rigidbody.useGravity = true;
        }

        public override void OnVault(PlayerController playerController, InputAction.CallbackContext context)
        {
        }

        public override void OnAim(PlayerController playerController, InputAction.CallbackContext context)
        {
        }

        public override void OnFire(PlayerController playerController, InputAction.CallbackContext context)
        {
        }
    }

    public class PlayerCarState : PlayerState
    {
        public NetworkVehicle Vehicle;
        public VehicleSeat Seat;

        private double _flipTimeBegin;

        public override void OnEnter(PlayerController playerController, PlayerState oldState)
        {
            playerController.Animator.SetBool(PlayerController.SittingHash, true);
            playerController.BodyTrackingLayer.active = true;
            playerController.IK.enabled = false;

            // prevent physics from messing with car
            playerController.GetComponent<Collider>().excludeLayers |= LayerMask.GetMask("Vehicle", "Ignore Raycast");
            if(playerController.isLocalPlayer) playerController.Rigidbody.isKinematic = true;

            // remove player hat
            var indexes = playerController.Info.CustomisationIndexes;
            playerController.Parts.Apply(new PartIndexes
            {
                CharacterIndex = indexes.CharacterIndex,
                MaterialIndex = indexes.MaterialIndex,
                HairIndex = indexes.HairIndex,
                HatIndex = -1
            });

            // disable weapon
            playerController.LoadoutManager.WeaponShown = false;

            // move player to seat
            Vehicle = playerController.CurrentVehicle.GetComponent<NetworkVehicle>();
            Seat = Vehicle.Seats[playerController.CurrentSeat];
            MovePlayerToSeat(playerController);

            // set camera target to vehicle transform
            if(playerController.isLocalPlayer)
            {
                playerController.CameraComponents.FreeLook.Follow = Vehicle.transform;
                playerController.CameraComponents.FreeLook.LookAt = Vehicle.transform;

                // set camera rig damping to 0
                SharedStateFunctionality.SetAllRigsDamping(playerController.CameraComponents.FreeLook.GetAllRigs(), 0f);

                // set to face forward
                playerController.AimTarget.SetLocalPositionAndRotation(new Vector3(0f, 0f, 3f), Quaternion.identity);
                playerController.CmdSetAimTarget(new Vector3(0f, 0f, 3f), Quaternion.identity);
            }
        }

        public override void OnExit(PlayerController playerController)
        {
            playerController.Animator.SetBool(PlayerController.SittingHash, false);
            playerController.BodyTrackingLayer.active = false;
            playerController.IK.enabled = true;

            var exitPosition = Seat.transform.position + Seat.transform.right * (Seat.Side == SeatSide.Left ? -2f : 2f);
            playerController.transform.position = new Vector3(exitPosition.x, playerController.transform.position.y, exitPosition.z);

            if(playerController.isLocalPlayer)
            {
                playerController.Rigidbody.rotation = Quaternion.Euler(0f, playerController.transform.rotation.eulerAngles.y, 0f);
                playerController.Rigidbody.isKinematic = false;

                playerController.Rigidbody.position = playerController.transform.position;

                playerController.CameraComponents.FreeLook.Follow = playerController.transform;
                playerController.CameraComponents.FreeLook.LookAt = playerController.transform;

                // set camera rig damping back
                SharedStateFunctionality.SetAllRigsDamping(playerController.CameraComponents.FreeLook.GetAllRigs(), 0.1f);
            }

            Physics.SyncTransforms();
            playerController.GetComponent<Collider>().excludeLayers &= ~LayerMask.GetMask("Vehicle", "Ignore Raycast");

            // reapply player hat
            playerController.Parts.Apply(playerController.Info.CustomisationIndexes);

            // enable weapon
            playerController.LoadoutManager.WeaponShown = true;
        }

        public override void OnEnterVehicle(PlayerController playerController, InputAction.CallbackContext context)
        {
            if(!playerController.isLocalPlayer || PlayerController.DisableInput) return;

            if(context.action.WasPerformedThisFrame())
            {
                SharedStateFunctionality.TransferToAppropriateState(playerController);
                Vehicle.CmdLeave();
            }
        }

        // Mirror is abysmal and doesn't support nested network objs so we manually set position every frame
        private void MovePlayerToSeat(Component playerController) => playerController.transform.SetPositionAndRotation(Seat.transform.position, Seat.transform.rotation);

        public override void OnUpdate(PlayerController playerController)
        {
            MovePlayerToSeat(playerController);
        }

        public override void OnFixedUpdate(PlayerController playerController)
        {
            // Swimming check
            if(playerController.IsSubmerged && playerController.CurrentState is not PlayerStateSwimming)
            {
                playerController.UpdateState(playerController.SwimmingState);
                Vehicle.CmdLeave();
                return;
            }

            if(Mathf.Abs(Vehicle.transform.localEulerAngles.z) > 90f)
            {
                if(_flipTimeBegin == 0f)
                {
                    _flipTimeBegin = Time.timeAsDouble;
                }
                else if(Time.timeAsDouble - _flipTimeBegin >= 5f)
                {
                    Vehicle.GetComponent<Rigidbody>().rotation = Quaternion.Euler(0f, Vehicle.transform.rotation.eulerAngles.y, 0f);
                    _flipTimeBegin = 0;
                }
            }
            else
            {
                _flipTimeBegin = 0f;
            }
        }

        public override void OnVault(PlayerController playerController, InputAction.CallbackContext context)
        {
        }

        public override void OnAim(PlayerController playerController, InputAction.CallbackContext context)
        {
        }

        public override void OnFire(PlayerController playerController, InputAction.CallbackContext context)
        {
        }

        public override void OnReload(PlayerController playerController, InputAction.CallbackContext context)
        {
        }
    }

    public class PlayerStateDriver : PlayerCarState
    {
        public override void OnEnter(PlayerController playerController, PlayerState oldState)
        {
            base.OnEnter(playerController, oldState);

            if(playerController.isLocalPlayer) Vehicle.GetComponent<VehicleInput>().enabled = true;

            playerController.LeftHandConstraint.data.target = Vehicle.LeftWheelHandle;
            playerController.RightHandConstraint.data.target = Vehicle.RightWheelHandle;

            // hints are tuned for weapon-holding, look weird in car
            playerController.LeftHandConstraint.data.hintWeight = 0f;
            playerController.RightHandConstraint.data.hintWeight = 0f;

            playerController.VehiclePedalLayer.active = true;
            playerController.LeftFootConstraint.data.target = Vehicle.LeftPedal;
            playerController.RightFootConstraint.data.target = Vehicle.RightPedal;

            playerController.RebuildAnimator();
        }

        public override void OnExit(PlayerController playerController)
        {
            base.OnExit(playerController);

            if(playerController.isLocalPlayer) Vehicle.GetComponent<VehicleInput>().enabled = false;

            playerController.VehiclePedalLayer.active = false;

            playerController.LeftHandConstraint.data.target = playerController.LoadoutManager.CurrentWeapon.LeftHandIKTargetDown;
            playerController.RightHandConstraint.data.target = playerController.LoadoutManager.CurrentWeapon.RightHandIKTargetDown;

            playerController.LeftHandConstraint.data.hintWeight = 1f;
            playerController.RightHandConstraint.data.hintWeight = 1f;

            playerController.LeftFootConstraint.data.target = null;
            playerController.RightFootConstraint.data.target = null;
            playerController.RebuildAnimator();
        }
    }

    public class PlayerStatePassenger : PlayerCarState
    {
    }

    public class PlayerStateDead : PlayerState
    {
        public override void OnEnter(PlayerController playerController, PlayerState oldState)
        {
            if(playerController.isLocalPlayer)
            {
                playerController.NetworkAnimator.SetTrigger(PlayerController.DeathHash);
            }

            playerController.LoadoutManager.WeaponShown = false;
        }

        public override void OnExit(PlayerController playerController)
        {
            if(playerController.isLocalPlayer)
            {
                playerController.NetworkAnimator.ResetTrigger(PlayerController.DeathHash);
            }

            playerController.LoadoutManager.WeaponShown = true;
        }

        public override void OnUpdate(PlayerController playerController)
        {
            if(!playerController.isLocalPlayer || PlayerController.DisableInput) return;

            if(playerController.PlayerInput.actions["Respawn"].WasPressedThisFrame() && KilledBar.Instance.RespawnTime <= 0) playerController.CmdRespawn();
        }

        public override void OnFixedUpdate(PlayerController playerController)
        {
        }

        public override void OnReload(PlayerController playerController, InputAction.CallbackContext context)
        {
        }

        public override void OnAim(PlayerController playerController, InputAction.CallbackContext context)
        {
        }

        public override void OnFire(PlayerController playerController, InputAction.CallbackContext context)
        {
        }

        public override void OnVault(PlayerController playerController, InputAction.CallbackContext context)
        {
        }
    }
}
