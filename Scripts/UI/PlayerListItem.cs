using TMPro;
using UnityEngine;

namespace UI
{
    public class PlayerListItem : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _playerNameText;

        [SerializeField]
        private TextMeshProUGUI _playerPingText;

        public void SetPlayerName(string playerName) => _playerNameText.text = playerName;
        
        public void SetPing(int ping) => _playerPingText.text = ping + "ms";
    }
}
