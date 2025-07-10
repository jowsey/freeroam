using System.Collections.Generic;
using System.Linq;
using ElRaccoone.Tweens;
using ElRaccoone.Tweens.Core;
using Mirror;
using Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UI
{
    public class PlayerList : BlockNetIdentityFromChangingTransform
    {
        public static PlayerList Instance { get; private set; }

        private PlayerInput _playerInput;

        [SerializeField]
        private PlayerListItem _playerListItemPrefab;

        private readonly IDictionary<uint, PlayerListItem> _playerListItems = new Dictionary<uint, PlayerListItem>();

        private readonly SyncDictionary<uint, int> _playerPings = new();

        private bool _toggled;

        private new void Awake()
        {
            base.Awake();
            Instance = this;
            
            GetComponent<CanvasGroup>().alpha = 0f;

            PlayerController.LocalPlayerJoined.AddListener(lp =>
            {
                _playerInput = lp.PlayerInput;
                _playerInput.actions["ShowPlayerList"].performed += TogglePlayerList;
                _playerInput.actions["ShowPlayerList"].canceled += TogglePlayerList;

                _playerPings.Callback += OnPlayerPingsChanged;
            });
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();

            ((Core.NetworkManager) NetworkManager.singleton).OnPlayerJoins.AddListener(conn => _playerPings.Add(conn.identity.netId, 0));
            ((Core.NetworkManager) NetworkManager.singleton).OnPlayerLeaves.AddListener(conn =>
            {
                if(conn.identity) _playerPings.Remove(conn.identity.netId);
            });
        }

        private void TogglePlayerList(InputAction.CallbackContext obj) => TogglePlayerList();

        private void TogglePlayerList()
        {
            if(_playerInput == null) return; // mfw unity
            gameObject.TweenCancelAll();
            
            if(_toggled) // hide
            {
                gameObject.TweenCanvasGroupAlpha(0f, 0.1f)
                    .SetEase(EaseType.CubicInOut)
                    .SetOnComplete(() => _toggled = false)
                    .SetOnCancel(() => _toggled = false);
            }
            else if(!PauseMenu.IsPaused) // show
            {
                _toggled = true;
                gameObject.TweenCanvasGroupAlpha(1f, 0.1f)
                    .SetEase(EaseType.CubicInOut);
            }
        }

        /// <summary>
        /// Update the player list for all players with a new ping for a given player
        /// </summary>
        /// <param name="sender">Client that sent the command</param>
        /// <param name="ping">Ping in milliseconds. Set to -1 to remove from list</param>
        [Command(requiresAuthority = false)]
        public void CmdSetPing(int ping, NetworkConnectionToClient sender = null)
        {
            if(ping == -1)
            {
                _playerPings.Remove(sender!.identity.netId);
            }
            else
            {
                _playerPings[sender!.identity.netId] = ping;
            }
        }

        public void AddPlayerListItem(uint playerNetId)
        {
            _playerListItems[playerNetId] = Instantiate(_playerListItemPrefab, transform);

            _playerListItems[playerNetId].SetPlayerName(
                FindObjectsByType<NetworkIdentity>(FindObjectsSortMode.None)
                    .First(i => i.netId == playerNetId)
                    .GetComponent<PlayerController>().Info.Name
            );
        }

        public void RemovePlayerListItem(uint playerNetId)
        {
            if(!_playerListItems.ContainsKey(playerNetId)) return;

            Destroy(_playerListItems[playerNetId].gameObject);
            _playerListItems.Remove(playerNetId);
        }

        /// <summary>
        /// Called on clients when the ping list is updated
        /// </summary>
        /// <param name="op">What operation performed on list</param>
        /// <param name="key">The player net id that changed</param>
        /// <param name="item">The new ping value</param>
        public void OnPlayerPingsChanged(SyncIDictionary<uint, int>.Operation op, uint key, int item)
        {
            switch (op)
            {
                // case SyncIDictionary<uint, int>.Operation.OP_ADD:
                //     AddPlayerListItem(key);
                //     _playerListItems[key].SetPing(item);
                //     break;
                // case SyncIDictionary<uint, int>.Operation.OP_REMOVE:
                //     RemovePlayerListItem(key);
                //     break;
                case SyncIDictionary<uint, int>.Operation.OP_SET:
                    _playerListItems[key].SetPing(item);
                    break;
                // case SyncIDictionary<uint, int>.Operation.OP_CLEAR:
                //     foreach (var playerListItem in _playerListItems) RemovePlayerListItem(playerListItem.Key);
                //     break;
            }
        }
    }
}
