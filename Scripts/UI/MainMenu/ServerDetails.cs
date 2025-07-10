using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace UI.MainMenu
{
    public class ServerDetails : MonoBehaviour
    {
        [FormerlySerializedAs("_serverIconImage")]
        public Image ServerIconImage;

        [FormerlySerializedAs("_serverNameText")]
        public TextMeshProUGUI ServerNameText;

        [FormerlySerializedAs("_serverDetailsText")]
        public TextMeshProUGUI ServerDetailsText;

        [FormerlySerializedAs("_joinButton")]
        public Button JoinButton;

        [HideInInspector]
        public EOSLobby _eosLobby;

        public static void DisableAllJoinButtons()
        {
            var serverDetails = FindObjectsOfType<ServerDetails>();
            
            foreach (var details in serverDetails) details.JoinButton.interactable = false;
        }
        
        public static void EnableAllJoinButtons()
        {
            var serverDetails = FindObjectsOfType<ServerDetails>();
            
            foreach (var details in serverDetails) details.JoinButton.interactable = true;
        }
    }
}
