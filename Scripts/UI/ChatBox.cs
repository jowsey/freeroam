using ElRaccoone.Tweens;
using ElRaccoone.Tweens.Core;
using Mirror;
using Player;
using TMPro;
using UnityEngine;
using Cursor = UnityEngine.Cursor;

namespace UI
{
    public class ChatBox : BlockNetIdentityFromChangingTransform
    {
        public static ChatBox Instance;

        public TextMeshProUGUI HistoryText;
        public TMP_InputField InputField;
        
        private CanvasGroup _canvasGroup;

        [Command(requiresAuthority = false)]
        public void CmdSendPlayerMessage(string message, NetworkConnectionToClient sender = null)
        {
            if(string.IsNullOrWhiteSpace(message)) return;
            if(message.Length > InputField.characterLimit) message = message[..120];
            message = message.Replace("<", "\\<").Replace(">", "\\>");

            RpcSendMessage($"<b>{sender!.identity.GetComponent<PlayerController>().Info.Name}</b> {message}");
        }

        [Server]
        public void SendServerMessage(string message)
        {
            RpcSendMessage($"(!) <color=#F1BA32>{message}</color>");
        }

        [Server]
        [ClientRpc]
        public void RpcSendMessage(string message)
        {
            HistoryText.text += $"{message}\n";
        }

        private new void Awake()
        {
            base.Awake();
            
            Instance = this;

            _canvasGroup = GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0.5f;
            
            InputField.onSubmit.AddListener(s =>
            {
                if(!InputField.isFocused || string.IsNullOrWhiteSpace(InputField.text))
                {
                    Activate();
                    return;
                };
                
                CmdSendPlayerMessage(s);
                InputField.text = "";
                Deactivate();
            });
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            HistoryText.text = "";

            PlayerController.LocalPlayerJoined.AddListener(lp =>
            {
                lp.PlayerInput.actions["FocusChat"].performed += ctx =>
                {
                    if(!ctx.action.WasPressedThisFrame() || InputField.isFocused || PauseMenu.IsPaused) return;

                    Activate();
                };
                
                lp.PlayerInput.actions["UnfocusChat"].performed += ctx =>
                {
                    if(!ctx.action.WasPressedThisFrame() || !InputField.isFocused) return;

                    Deactivate();
                };
            });
        }

        private void Activate()
        {
            _canvasGroup.TweenCanvasGroupAlpha(1f, 0.1f).SetEase(EaseType.CubicInOut);

            InputField.interactable = true;
            InputField.ActivateInputField();
            Cursor.lockState = CursorLockMode.None;
        }

        private void Deactivate()
        {
            _canvasGroup.TweenCanvasGroupAlpha(0.5f, 0.1f).SetEase(EaseType.CubicInOut);

            InputField.interactable = false;
            InputField.DeactivateInputField();
            Cursor.lockState = CursorLockMode.Locked;
        }

        // private void Update()
        // {
        //     if(InputField.IsActive() && PauseMenu.IsPaused) InputField.DeactivateInputField();
        // }
    }
}
