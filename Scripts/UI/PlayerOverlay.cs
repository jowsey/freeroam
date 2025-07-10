using Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class PlayerOverlay : MonoBehaviour
    {
        [HideInInspector]
        public PlayerController LinkedPlayer;

        public TextMeshProUGUI NameText;
        public TextMeshProUGUI HealthText;
        public Image HealthBarImage;
        public CanvasGroup CanvasGroup;
        
        private void Start()
        {
            NameText.text = LinkedPlayer.Info.Name;
        }

        private void Update()
        {
            // HealthText.text = $"{LinkedPlayer.Stats.CurrentHealth}/{LinkedPlayer.Stats.MaxHealth}";
            // HealthBarImage.fillAmount = (float) LinkedPlayer.Stats.CurrentHealth / LinkedPlayer.Stats.MaxHealth;
        }
    }
}
