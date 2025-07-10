using Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UI
{
    public class KilledBar : MonoBehaviour
    {
        public static KilledBar Instance { get; private set; }

        public TextMeshProUGUI TitleText;

        public TextMeshProUGUI RespawnTimerText;

        public DamageContext Context;

        [HideInInspector]
        public float RespawnTime;

        private void Awake() => Instance = this;

        private void Start()
        {
            var rivalName = Context.Rival != null ? Context.Rival.GetComponent<PlayerController>().Info.Name : "world";
            var verb = Context.Type.ToString().ToLower();

            var weaponNameStartsWithVowel = "aeiou".Contains(Context.Weapon[0].ToString().ToLower());

            TitleText.text = $"Killed by <color=#DD2222>{rivalName}</color>\n" +
                             $"<size=50%><color=#DDDDDD>{verb} with {(weaponNameStartsWithVowel ? "an" : "a")} {Context.Weapon}{(Context.Distance != 0 ? $" @ {Mathf.Floor(Context.Distance)}m" : "")}</color></size>";
        }

        private void Update()
        {
            var bindingDisplayString = PlayerController.LocalPlayer.PlayerInput.actions["Respawn"].GetBindingDisplayString();

            RespawnTimerText.text = RespawnTime > 0
                ? $"<color=#DDDDDD>Respawn in {Mathf.Ceil(RespawnTime -= Time.deltaTime)}s</color>"
                : $"Press [{bindingDisplayString}] to respawn";
        }
    }
}
