using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Cinemachine;
using Mirror;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Weapons;
using Core;
using ElRaccoone.Tweens;
using ElRaccoone.Tweens.Core;
using EVP;
using FischlWorks;
using JetBrains.Annotations;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Vehicles;
using NetworkManager = Mirror.NetworkManager;

namespace Player
{
    public class CameraComponents
    {
        public Camera Camera;
        public CinemachineCameraOffset CameraOffset;
        public CinemachineFreeLook FreeLook;
        public CinemachineInputProvider InputProvider;
        public CinemachineCollider Collider;
    }

    [Serializable]
    public class UIComponents
    {
        public Image Crosshair;
        public Image Hitmarker;
        public Image KillHitmarker;
        public TextMeshProUGUI AmmoCounter;

        [HideInInspector]
        public KilledBar KilledBar;
    }

    [Serializable]
    public class PlayerInfo
    {
        public string Name;
        public PartIndexes CustomisationIndexes;
    }

    public class PlayerController : NetworkBehaviour
    {
        public static PlayerController LocalPlayer { get; private set; }

        /// <summary>
        /// The local player isn't set for the first few frames as some network requests need to be sent beforehand
        /// </summary>
        public static readonly UnityEvent<PlayerController> LocalPlayerJoined = new();

        public static PlayerController[] Players => FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        #region Components

        public Animator Animator { get; private set; }

        public NetworkAnimator NetworkAnimator { get; private set; }

        public static readonly int MoveForwardsHash = Animator.StringToHash("MoveForwards");
        public static readonly int MoveSidewaysHash = Animator.StringToHash("MoveSideways");
        public static readonly int SprintingHash = Animator.StringToHash("Sprinting");
        public static readonly int FallingHash = Animator.StringToHash("Falling");
        public static readonly int AimingHash = Animator.StringToHash("Aiming");
        public static readonly int DeathHash = Animator.StringToHash("Death");
        public static readonly int SwimmingHash = Animator.StringToHash("Swimming");
        public static readonly int SittingHash = Animator.StringToHash("Sitting");

        public PlayerInput PlayerInput { get; private set; }

        public static bool DisableInput => PauseMenu.IsPaused || AttachmentsMenu.IsOpen || ChatBox.Instance.InputField.isFocused;

        public InputAction MovementAction { get; private set; }
        public InputAction SprintAction { get; private set; }
        public InputAction FireAction { get; private set; }

        public Rigidbody Rigidbody { get; private set; }

        public csHomebrewIK IK { get; private set; }

        public bool IsGrounded => Physics.CheckSphere(transform.position, 0.25f, ~LayerMask.GetMask("Player"));

        public bool IsSubmerged => Physics.CheckSphere(transform.position, 0.25f, LayerMask.GetMask("Water"));

        public CameraComponents CameraComponents { get; private set; }

        #endregion

        #region Player

        [SyncVar(hook = nameof(OnPlayerInfoChanged))]
        public PlayerInfo Info;

        public PlayerStats Stats { get; private set; }

        public CharacterParts Parts { get; private set; }


        [Command]
        private void CmdSetPlayerInfo(PlayerInfo info)
        {
            if(!string.IsNullOrEmpty(Info.Name)) return; // only allow setting on initial load (prevents confusion with randomly changing player names)

            info.Name = (info.Name.Length > 20 ? info.Name[..20] : info.Name).Trim();
            Info = info;

            ChatBox.Instance.SendServerMessage($"{Info.Name} joined the game");
        }

        public void OnPlayerInfoChanged(PlayerInfo oldData, PlayerInfo newData)
        {
            if(!isLocalPlayer) // if local player, we're changing scene anyway so this is taken care of by unity (results in error otherwise)
            {
                PlayerOverlayManager.Instance.RemoveOverlay(this);
                PlayerOverlayManager.Instance.AddOverlay(this);
            }

            PlayerList.Instance.RemovePlayerListItem(netId);
            PlayerList.Instance.AddPlayerListItem(netId);

            Parts.Apply(newData.CustomisationIndexes);
        }

        #endregion

        #region Weapons

        [Header("Weapons")]
        public Transform WeaponRigContainer;

        public PlayerLoadoutManager LoadoutManager { get; private set; }

        [HideInInspector]
        public RigBuilder RigBuilder;

        [HideInInspector]
        public RigLayer BodyTrackingLayer;

        [HideInInspector]
        public RigLayer GunPointingLayer;

        [HideInInspector]
        public RigLayer GunHoldingLayer;

        [HideInInspector]
        public RigLayer VehiclePedalLayer;

        public UIComponents UIComponents;

        public Transform AimTarget;

        [FormerlySerializedAs("AimTargetPos")]
        [SyncVar(hook = nameof(OnAimTargetLocalPosChanged))]
        public Vector3 AimTargetLocalPos;

        [FormerlySerializedAs("AimTargetRot")]
        [SyncVar(hook = nameof(OnAimTargetLocalRotChanged))]
        public Quaternion AimTargetLocalRot;

        public TwoBoneIKConstraint LeftHandConstraint;
        public TwoBoneIKConstraint RightHandConstraint;

        public TwoBoneIKConstraint LeftFootConstraint;
        public TwoBoneIKConstraint RightFootConstraint;

        private double _deathTime;

        public void UpdateLoadout(LoadoutSlot slot, WeaponType weaponType) => CmdSetLoadoutSlot(slot, weaponType);

        [Command]
        private void CmdSetLoadoutSlot(LoadoutSlot slot, WeaponType weaponType) => RpcSetLoadoutSlot(slot, weaponType);

        [ClientRpc]
        public void RpcSetLoadoutSlot(LoadoutSlot slot, WeaponType weaponType)
        {
            // Stop aiming if we currently are
            if(isLocalPlayer && CurrentState is PlayerStateAiming) SharedStateFunctionality.TransferToAppropriateState(this);

            // Remove existing weapon gameobject
            if(LoadoutManager.Weapons[(int) slot] != null)
            {
                if(LoadoutManager.ActiveLoadoutSlot == slot)
                {
                    LoadoutManager.Weapons[(int) slot].gameObject.SetActive(false);
                    Destroy(LoadoutManager.Weapons[(int) slot].gameObject, 0.1f); // delay destroy to prevent animation rig complaining
                }
                else
                {
                    Destroy(LoadoutManager.Weapons[(int) slot].gameObject);
                }
            }

            // Craete new weapon
            if(weaponType != WeaponType.None)
            {
                var weapon = weaponType.InstantiateFromType(WeaponRigContainer);
                LoadoutManager.Weapons[(int) slot] = weapon;

                if(isServer)
                {
                    weapon.WeaponData.CurrentAmmo = weapon.MaxAmmo;
                    LoadoutManager.WeaponData[(int) slot] = weapon.WeaponData;
                }

                if(LoadoutManager.ActiveLoadoutSlot == slot && LoadoutManager.WeaponShown)
                {
                    LoadoutManager.EquipWeapon(LoadoutManager.Weapons[(int) slot]);
                }
                else
                {
                    LoadoutManager.Weapons[(int) slot].gameObject.SetActive(false);
                }

                weapon.transform.SetSiblingIndex((int) slot);
            }
            else
            {
                LoadoutManager.Weapons[(int) slot] = null;
                LoadoutManager.WeaponData[(int) slot] = null;

                LoadoutManager.EquipWeapon(null);
            }
        }

        // todo   kinda hacky, would be cleaner to make a new state for hip-firing but that would be repeating
        // todo   a ton of code since it's basically one if-statement of difference in the aiming state
        public void NetworkSetHipFiring(bool isHipFiring)
        {
            ((PlayerStateAiming) AimingState).IsHipFiring = isHipFiring;
            CmdSetIsHipFiring(isHipFiring);
        }

        [Command]
        private void CmdSetIsHipFiring(bool isHipFiring) => RpcSetIsHipFiring(isHipFiring);

        [ClientRpc(includeOwner = false)]
        private void RpcSetIsHipFiring(bool isHipFiring) => ((PlayerStateAiming) AimingState).IsHipFiring = isHipFiring;

        public void NetworkFire()
        {
            CmdFire();
            LoadoutManager.CurrentWeapon.LastFiredTimestamp = NetworkTime.time;
        }

        [Command(channel = Channels.Unreliable)]
        private async void CmdFire()
        {
            if(LoadoutManager.CurrentWeapon == null || LoadoutManager.CurrentAmmo == 0) return;

            // Validate weapon fire-rate
            // 25ms leeway to account for network latency, this potentially allows cheaters to increase fire rate slightly but
            // they would also have to have really good timing on the network or like something idk im not a network anticheat
            // engineer i just work here bro
            if(NetworkTime.time - LoadoutManager.CurrentWeapon.LastFiredTimestamp < LoadoutManager.CurrentWeapon.TimeBetweenShots - 0.025f) return;
            LoadoutManager.CurrentWeapon.LastFiredTimestamp = NetworkTime.time;

            // reduce ammo
            LoadoutManager.CurrentAmmo--;

            // don't allow shooting self
            gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

            // camera obj isn't synced to server (for perf), instead we infer camera pos + rot from aimtarget
            var ray = new Ray(AimTarget.transform.position - AimTarget.transform.forward * 10f, AimTarget.transform.forward);
            var raycast = Physics.Raycast(ray, out var hit, 1000f);

            RpcFire(raycast ? hit.point : ray.GetPoint(1000f));

            // re-add layer after raycast is done
            gameObject.layer = LayerMask.NameToLayer("Player");

            if(!raycast || !hit.collider.TryGetComponent(out PlayerController targetPlayer) || targetPlayer.Stats.IsDead) return;

            // calculate damage falloff
            var distance = Vector3.Distance(targetPlayer.transform.position, transform.position);

            var damage = LoadoutManager.CurrentWeapon.ModifiedDamage;
            var falloffBegin = LoadoutManager.CurrentWeapon.ModifiedDamageFalloffBegin;
            var falloffEnd = LoadoutManager.CurrentWeapon.ModifiedDamageFalloffEnd;

            damage = Mathf.Lerp(
                damage,
                damage * (LoadoutManager.CurrentWeapon.DamageFalloffPercent / 100),
                (distance - falloffBegin) / (falloffEnd - falloffBegin)
            );

            if(_debugLogging) Debug.Log($"Dealing {damage} damage to '{targetPlayer.Info.Name}' ⟶ {distance}m");

            var damageContext = new DamageContext
            {
                Type = DamageContext.DamageType.Shot,
                Rival = netIdentity,
                Damage = damage,
                Distance = distance,
                Weapon = LoadoutManager.CurrentWeapon.GetWeaponName()
            };

            targetPlayer.Stats.DealDamage(damageContext);

            if(targetPlayer.Stats.CurrentHealth <= 0)
            {
                targetPlayer.RpcSetState(GetStateIndex(DeadState));

                TargetHitmarker(connectionToClient, HitmarkerType.Kill);
                targetPlayer._deathTime = NetworkTime.time;
            }
            else
            {
                TargetHitmarker(connectionToClient, HitmarkerType.Damage);
            }

            // create blood splatter
            var bloodSplat = await Addressables.InstantiateAsync("FX/BloodSplat", hit.point, Quaternion.LookRotation(hit.normal)).Task;
            NetworkServer.Spawn(bloodSplat);

            await Task.Delay(1000);

            NetworkServer.Destroy(bloodSplat);
        }

        [ClientRpc(channel = Channels.Unreliable)]
        private void RpcFire(Vector3 hitPoint) => LoadoutManager.CurrentWeapon.Fire(hitPoint);

        [TargetRpc]
        // ReSharper disable once UnusedParameter.Local
        private async void TargetHitmarker(NetworkConnectionToClient target, HitmarkerType type)
        {
            switch (type)
            {
                case HitmarkerType.Damage:
                    AudioSource.PlayClipAtPoint(Addressables.LoadAssetAsync<AudioClip>("Audio/Hitmarker").WaitForCompletion(), LoadoutManager.CurrentWeapon.transform.position);
                    UIComponents.Hitmarker.gameObject.SetActive(true);
                    await Task.Delay(25);
                    UIComponents.Hitmarker.gameObject.SetActive(false);
                    break;
                case HitmarkerType.Kill:
                    AudioSource.PlayClipAtPoint(Addressables.LoadAssetAsync<AudioClip>("Audio/KillHitmarker").WaitForCompletion(), LoadoutManager.CurrentWeapon.transform.position);
                    UIComponents.KillHitmarker.gameObject.SetActive(true);
                    await Task.Delay(25);
                    UIComponents.KillHitmarker.gameObject.SetActive(false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        [TargetRpc]
        private void TargetDie(DamageContext context)
        {
            var killer = context.Rival.GetComponent<PlayerController>();

            if(_debugLogging) Debug.Log($"Killed by {killer.Info.Name} with {context.Weapon} ⟶ {context.Distance}m");

            UIComponents.KilledBar = Addressables.InstantiateAsync("UI/KilledBar", FindObjectOfType<Canvas>().transform).WaitForCompletion().GetComponent<KilledBar>();

            UIComponents.KilledBar.Context = context;
            UIComponents.KilledBar.RespawnTime = 7f; // todo dont hardcode

            Cursor.lockState = CursorLockMode.None;
            CameraComponents.FreeLook.LookAt = killer.transform;
            CameraComponents.Collider.enabled = false;
        }

        [Command]
        public void CmdRespawn()
        {
            if(CurrentState is PlayerStateDead && Stats.IsDead && NetworkTime.time - _deathTime >= 7f)
            {
                Stats.SetHealth(Stats.MaxHealth);
                Stats.SetArmour(Stats.MaxArmour);
                LoadoutManager.RpcInstantReloadAll();

                TargetRespawn();
            }
        }

        [TargetRpc]
        private void TargetRespawn()
        {
            UpdateState(IdleState);
            Destroy(UIComponents.KilledBar.gameObject);

            Cursor.lockState = CursorLockMode.Locked;
            CameraComponents.FreeLook.LookAt = transform;
            CameraComponents.Collider.enabled = true;

            var startPosition = NetworkManager.singleton.GetStartPosition();
            transform.SetPositionAndRotation(startPosition.position, startPosition.rotation);
        }

        [Command]
        public void CmdSetAimTarget(Vector3 localPos, Quaternion localRot)
        {
            AimTargetLocalPos = localPos;
            AimTargetLocalRot = localRot;
        }

        private void OnAimTargetLocalPosChanged(Vector3 oldValue, Vector3 newValue) => AimTarget.localPosition = newValue;

        private void OnAimTargetLocalRotChanged(Quaternion oldValue, Quaternion newValue) => AimTarget.localRotation = newValue;

        #endregion

        #region State machine

        public PlayerState CurrentState;

        public readonly PlayerState IdleState = new PlayerStateIdle();
        public readonly PlayerState WalkingState = new PlayerStateWalking();
        public readonly PlayerState SprintingState = new PlayerStateSprinting();
        public readonly PlayerState AimingState = new PlayerStateAiming();
        public readonly PlayerState VaultingState = new PlayerStateVaulting();
        public readonly PlayerState FallingState = new PlayerStateFalling();
        public readonly PlayerState ReloadingState = new PlayerStateReloading();
        public readonly PlayerState SwimmingState = new PlayerStateSwimming();
        public readonly PlayerState DriverState = new PlayerStateDriver();
        public readonly PlayerState PassengerState = new PlayerStatePassenger();
        public readonly PlayerState DeadState = new PlayerStateDead();

        private readonly List<PlayerState> States = new();

        /// <summary>
        /// Relies on input etc so we ask client to run it instead of running on server
        /// </summary>
        [TargetRpc]
        public void TargetTransferToAppropriateState() => SharedStateFunctionality.TransferToAppropriateState(this);

        private int GetStateIndex(PlayerState state) => States.IndexOf(state);

        public void UpdateState(PlayerState state)
        {
            SetState(state);
            CmdSetState(GetStateIndex(state));
        }

        [Command]
        private void CmdSetState(int index)
        {
            if(!Stats.IsDead) RpcSetState(index);
        }

        [ClientRpc]
        private void RpcSetState(int index) => SetState(States[index]);

        private void SetState(PlayerState newState)
        {
            // var aimingToAiming = CurrentState is PlayerStateAiming && newState is PlayerStateAiming; // aiming has multiple ways to enter/exit so gets a workaround
            if(CurrentState == newState) return;

            if(_debugLogging) Debug.Log($"Setting state to {newState.GetType().Name} [from {CurrentState?.GetType().Name ?? "null"}]");

            CurrentState?.OnExit(this);

            var oldState = CurrentState;
            CurrentState = newState;

            CurrentState.OnEnter(this, oldState);
        }

        [SerializeField]
        private bool _debugLogging;

        #endregion

        #region Movement

        [Header("Movement")]
        public float BaseMovementSpeed = 3.6f;

        public float MovementSpeedModifier = 1f;

        public float SprintingSpeedModifier = 2.2f;

        public float AimingSpeedModifier = 0.7f;

        public float CameraAimSensModifier = 0.6f;

        [HideInInspector]
        public Vector3 MoveInputDir;

        [HideInInspector]
        public Vector3 LastRbPos;

        [HideInInspector]
        public float LastGroundedTime;

        [HideInInspector]
        public float LastJumpTime;

        public float JumpCooldown = 1f;

        public float JumpForce = 2f;

        [Command]
        public void CmdVault(Vector3 position, Vector3 normal) => RpcSetVaultData(position, normal);

        [ClientRpc]
        private void RpcSetVaultData(Vector3 position, Vector3 normal)
        {
            ((PlayerStateVaulting) VaultingState).LedgePosition = position;
            ((PlayerStateVaulting) VaultingState).LedgeNormal = normal;

            SetState(VaultingState);
        }

        #endregion

        #region Vehicles

        [SyncVar(hook = nameof(OnCurrentSeatChanged))]
        public int CurrentSeat;

        [SyncVar(hook = nameof(OnCurrentVehicleChanged))]
        public NetworkIdentity CurrentVehicle;

        public void OnCurrentSeatChanged(int _, int newSeat)
        {
            // todo implement
        }

        public void OnCurrentVehicleChanged(NetworkIdentity oldVehicle, NetworkIdentity newVehicle)
        {
            if(newVehicle != null)
            {
                var seats = newVehicle.GetComponent<NetworkVehicle>().Seats;
                var seat = seats[CurrentSeat];
                SetState(seat.IsDriverSeat ? DriverState : PassengerState);

                var vehicleAudio = newVehicle.GetComponent<VehicleAudio>();
                if(!vehicleAudio.enabled)
                {
                    vehicleAudio.enabled = true;
                    vehicleAudio.TweenValueFloat(vehicleAudio.engine.idlePitch, 1.75f, val => vehicleAudio.engine.idlePitch = val)
                        .SetFrom(0f)
                        .SetEase(EaseType.BackOut);
                }
            }
            else
            {
                // var vehicleAudio = oldVehicle.GetComponent<VehicleAudio>();
                // var seats = oldVehicle.GetComponent<NetworkVehicle>().Seats;

                // if(seats.All(s => s.TakenBy == null)) vehicleAudio.enabled = false;
            }
        }

        #endregion

        public void RebuildAnimator()
        {
            Animator.enabled = false;

            // get all parameters and save to rebind later
            var initialParameters = new Dictionary<string, object>();

            foreach (var parameter in Animator.parameters)
            {
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Bool:
                        initialParameters.Add(parameter.name, Animator.GetBool(parameter.name));
                        break;
                    case AnimatorControllerParameterType.Float:
                        initialParameters.Add(parameter.name, Animator.GetFloat(parameter.name));
                        break;
                    case AnimatorControllerParameterType.Int:
                        initialParameters.Add(parameter.name, Animator.GetInteger(parameter.name));
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        initialParameters.Add(parameter.name, Animator.GetBool(parameter.name));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            // unbind all handles
            Animator.UnbindAllStreamHandles();
            Animator.UnbindAllSceneHandles();

            // rebind
            typeof(Animator)
                .GetMethod(nameof(Animator.Rebind), BindingFlags.NonPublic | BindingFlags.Instance)?
                .Invoke(Animator, new object[] {false});

            // rebuild animation rigging
            RigBuilder.Build();
            RigBuilder.Evaluate(Time.deltaTime);

            // rebind parameters
            foreach (var parameter in initialParameters)
            {
                switch (parameter.Value)
                {
                    case bool b:
                        Animator.SetBool(parameter.Key, b);
                        break;
                    case float f:
                        Animator.SetFloat(parameter.Key, f);
                        break;
                    case int i:
                        Animator.SetInteger(parameter.Key, i);
                        break;
                }
            }

            Animator.enabled = true;
        }

        private void Awake()
        {
            // States
            foreach (var state in GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                if(state.FieldType == typeof(PlayerState))
                    States.Add((PlayerState) state.GetValue(this));

            // Components
            Animator = GetComponent<Animator>();
            NetworkAnimator = GetComponent<NetworkAnimator>();
            Rigidbody = GetComponent<Rigidbody>();

            IK = GetComponent<csHomebrewIK>();

            Stats = GetComponent<PlayerStats>();
            Parts = GetComponent<CharacterParts>();

            LoadoutManager = GetComponent<PlayerLoadoutManager>();

            // Input
            PlayerInput = GetComponent<PlayerInput>();
            MovementAction = PlayerInput.actions["Movement"];
            SprintAction = PlayerInput.actions["Sprint"];
            FireAction = PlayerInput.actions["Fire"];

            // Animation
            RigBuilder = GetComponent<RigBuilder>();
            BodyTrackingLayer = RigBuilder.layers[0];
            GunPointingLayer = RigBuilder.layers[1];
            GunHoldingLayer = RigBuilder.layers[2];
            VehiclePedalLayer = RigBuilder.layers[3];
        }

        private void Start()
        {
            if(isLocalPlayer)
            {
                LocalPlayer = this;
                LocalPlayerJoined.Invoke(this);

                // Camera
                CameraComponents = new CameraComponents
                {
                    Camera = Camera.main,
                    CameraOffset = FindAnyObjectByType<CinemachineCameraOffset>(),
                    FreeLook = FindAnyObjectByType<CinemachineFreeLook>(),
                    InputProvider = FindAnyObjectByType<CinemachineInputProvider>(),
                    Collider = FindAnyObjectByType<CinemachineCollider>(),
                };

                CameraComponents.FreeLook.Follow = transform;
                CameraComponents.FreeLook.LookAt = transform;

                // Set camera sensitivity
                var sens = PlayerPrefs.GetFloat("MouseSensitivity");
                var lookAction = PlayerInput.actions["Look"];
                lookAction.ApplyBindingOverride(new InputBinding {overrideProcessors = $"scaleVector2(x={sens},y={sens})"});

                UpdateState(IdleState);

                UpdateLoadout(LoadoutSlot.Primary, WeaponType.AssaultRifle);
                UpdateLoadout(LoadoutSlot.Secondary, WeaponType.CombatPistol);

                PlayerInput.enabled = true;
                Cursor.lockState = CursorLockMode.Locked;

                CmdSetPlayerInfo(new PlayerInfo
                {
                    Name = PlayerPrefs.GetString("PlayerName", "Player"),
                    CustomisationIndexes = Parts.GetPartsFromPrefs()
                });

                SetPlayerListPingLoop();
            }
            else
            {
                // don't need clientside UI for other players
                Destroy(UIComponents.Crosshair.transform.parent.gameObject);

                Destroy(Rigidbody); // pos comes from server anyway
                // todo look into network rigidbody?
            }
        }

        private async void SetPlayerListPingLoop()
        {
            while (this)
            {
                await Task.Delay(5000);

                if(!PlayerList.Instance) return; // if left game between task delay and now
                PlayerList.Instance.CmdSetPing((int) (NetworkTime.rtt * 1000));
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            Stats.OnDeath.AddListener(TargetDie);
        }

        private void Update()
        {
            CurrentState?.OnUpdate(this);

            if(isLocalPlayer) CameraComponents.InputProvider.enabled = Cursor.lockState != CursorLockMode.None;
        }

        private void OnDisable()
        {
            if(!isLocalPlayer && PlayerOverlayManager.Instance && PlayerList.Instance)
            {
                PlayerOverlayManager.Instance.RemoveOverlay(this);
                PlayerList.Instance.RemovePlayerListItem(netId);
            }

            if(isServer) ChatBox.Instance.SendServerMessage($"{Info.Name} left the game");
        }

        private void FixedUpdate()
        {
            CurrentState?.OnFixedUpdate(this);
        }

        private void LateUpdate()
        {
            CurrentState?.OnLateUpdate(this);
        }

        public void OnMove(InputAction.CallbackContext callbackContext)
        {
            CurrentState?.OnMove(this, callbackContext);
        }

        public void OnLook(InputAction.CallbackContext callbackContext)
        {
            CurrentState?.OnLook(this, callbackContext);
        }

        public void OnAim(InputAction.CallbackContext callbackContext)
        {
            CurrentState?.OnAim(this, callbackContext);
        }

        public void OnFire(InputAction.CallbackContext callbackContext)
        {
            CurrentState?.OnFire(this, callbackContext);
        }

        public void OnSprint(InputAction.CallbackContext callbackContext)
        {
            CurrentState?.OnSprint(this, callbackContext);
        }

        public void OnVault(InputAction.CallbackContext callbackContext)
        {
            CurrentState?.OnVault(this, callbackContext);
        }

        public void OnReload(InputAction.CallbackContext callbackContext)
        {
            CurrentState?.OnReload(this, callbackContext);
        }

        public void OnEnterVehicle(InputAction.CallbackContext callbackContext)
        {
            CurrentState?.OnEnterVehicle(this, callbackContext);
        }

        private void OnCollisionEnter(Collision collision)
        {
            var vehicle = collision.gameObject.GetComponentInParent<NetworkVehicle>();
            if(vehicle == null || collision.relativeVelocity.magnitude < 5f || collision.rigidbody.velocity.magnitude < 5f) return;

            var driver = vehicle.Seats[0].TakenBy;

            if(isServer)
            {
                Stats.DealDamage(new DamageContext
                {
                    Damage = collision.relativeVelocity.magnitude,
                    Type = DamageContext.DamageType.Hit,
                    Rival = driver != null ? driver.netIdentity : null,
                    Weapon = NetworkVehicle.VehicleTypeToName(vehicle.Type)
                });

                if(Stats.CurrentHealth <= 0f) RpcSetState(GetStateIndex(DeadState));

                if(driver != null) driver.TargetHitmarker(driver.connectionToClient, HitmarkerType.Damage);
            }

            if(isLocalPlayer) Rigidbody.AddForce(collision.relativeVelocity * 0.5f + transform.up * 5f, ForceMode.Impulse);
        }

        #region Animator events

        [UsedImplicitly]
        public void LeftFootDown() => Footstep();

        [UsedImplicitly]
        public void RightFootDown() => Footstep();

        public void Footstep()
        {
            var moveSpeed = Mathf.Abs(Mathf.Max(Animator.GetFloat(MoveForwardsHash), Animator.GetFloat(MoveSidewaysHash)));
            AudioSource.PlayClipAtPoint(Addressables.LoadAssetAsync<AudioClip>("Audio/Footstep").WaitForCompletion(), transform.position, Mathf.Clamp01(0.5f * moveSpeed));
        }

        #endregion
    }
}
