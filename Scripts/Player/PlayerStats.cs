using System;
using ElRaccoone.Tweens;
using ElRaccoone.Tweens.Core;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

namespace Player
{
    [Serializable]
    public class DamageContext
    {
        public enum DamageType
        {
            Shot,
            Hit
        }

        public DamageType Type;
        public NetworkIdentity Rival;
        public float Distance;
        public float Damage;
        public string Weapon;
    }

    public class PlayerStats : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnHealthChanged))]
        public float CurrentHealth;

        [SyncVar]
        public float MaxHealth = 100;

        [SyncVar(hook = nameof(OnArmourChanged))]
        public float CurrentArmour;

        [SyncVar]
        public float MaxArmour = 20;

        [Header("UI")]
        public Image HealthBar;

        public Image HealthCatchupBar;

        public TextMeshProUGUI HealthText;

        public CanvasGroup HealthCanvasGroup;

        [Space]
        public Image ArmourBar;

        public Image ArmourCatchupBar;

        public TextMeshProUGUI ArmourText;

        public CanvasGroup ArmourCanvasGroup;

        public bool IsDead => CurrentHealth == 0f;

        private float _lastStatChangeTime;
        private float _lastDamageTime;

        private float _lastHealthChangeTime;
        private float _lastArmourChangeTime;

        public UnityEvent<DamageContext> OnDeath = new();

        // alpha that the UI will fade to when idle
        private const float _idleAlpha = 0.1f;

        /// <summary>
        /// Automatically splits damage between health and armour and clamps as required
        /// </summary>
        /// <param name="context">Cause of the damage</param>
        public void DealDamage(DamageContext context)
        {
            if(!isServer) return;

            _lastDamageTime = Time.time;

            var armourDamage = Mathf.Min(context.Damage, CurrentArmour);
            var healthDamage = Mathf.Min(context.Damage - armourDamage, CurrentHealth);

            if(armourDamage > 0f) SetArmour(CurrentArmour - armourDamage);
            if(healthDamage > 0f) SetHealth(CurrentHealth - healthDamage);

            if(CurrentHealth == 0f) OnDeath.Invoke(context);
        }

        public void SetHealth(float health)
        {
            if(!isServer) return;

            _lastHealthChangeTime = Time.time;
            CurrentHealth = Mathf.Clamp(health, 0, MaxHealth);
        }

        public void SetArmour(float armour)
        {
            if(!isServer) return;

            _lastArmourChangeTime = Time.time;
            CurrentArmour = Mathf.Clamp(armour, 0, MaxArmour);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            SetHealth(MaxHealth);
            SetArmour(MaxArmour);
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            HealthCanvasGroup.alpha = _idleAlpha;
            ArmourCanvasGroup.alpha = _idleAlpha;
        }

        private void Update()
        {
            // regen
            if(isServer && !IsDead)
            {
                // regen armour to full, >8s no damage, 3/s
                if(Time.time - _lastDamageTime > 8f && CurrentArmour < MaxArmour)
                    SetArmour(CurrentArmour + Time.deltaTime * 3f);

                // regen health to 20%, >10s no damage, 2/s
                if(Time.time - _lastDamageTime > 10f && CurrentHealth < MaxHealth * 0.2f && CurrentArmour == MaxArmour)
                    SetHealth(CurrentHealth + Time.deltaTime * 2f);

                // todo make configurable, server settings panel of some kind
            }

            if(!isLocalPlayer) return;

            // fade out UIs after 5 seconds of no stat change
            if(Time.time - _lastHealthChangeTime > 5f && HealthCanvasGroup.alpha == 1f) HealthCanvasGroup.TweenCanvasGroupAlpha(_idleAlpha, 3f).SetEase(EaseType.CubicInOut);
            if(Time.time - _lastArmourChangeTime > 5f && ArmourCanvasGroup.alpha == 1f) ArmourCanvasGroup.TweenCanvasGroupAlpha(_idleAlpha, 3f).SetEase(EaseType.CubicInOut);

            // delay catchup by 0.5s
            if(HealthBar.fillAmount > HealthCatchupBar.fillAmount)
                HealthCatchupBar.fillAmount = HealthBar.fillAmount;
            else if(Time.time - _lastHealthChangeTime > 0.5f)
                HealthCatchupBar.fillAmount = Mathf.Lerp(HealthCatchupBar.fillAmount, HealthBar.fillAmount, Time.deltaTime * 2f);

            if(ArmourBar.fillAmount > ArmourCatchupBar.fillAmount)
                ArmourCatchupBar.fillAmount = ArmourBar.fillAmount;
            else if(Time.time - _lastArmourChangeTime > 0.5f)
                ArmourCatchupBar.fillAmount = Mathf.Lerp(ArmourCatchupBar.fillAmount, ArmourBar.fillAmount, Time.deltaTime * 2f);
        }

        private void OnHealthChanged(float oldHealth, float newHealth)
        {
            if(!isLocalPlayer) return;
            _lastHealthChangeTime = Time.time;

            if(HealthCanvasGroup.alpha == _idleAlpha && newHealth < oldHealth)
            {
                HealthCanvasGroup.TweenCanvasGroupAlpha(1f, 0.2f)
                    .SetEase(EaseType.CubicInOut);
            }

            HealthBar.fillAmount = newHealth / MaxHealth;
            HealthText.text = $"<b>{newHealth:0}</b><color=#EEEEEE>/{MaxHealth:0}</color>";

            // vignette at low health
            Camera.main!.GetComponent<PostProcessVolume>().profile.GetSetting<Vignette>().intensity.value = Mathf.Lerp(0.5f, 0f, newHealth / (MaxHealth * 0.2f));
        }

        private void OnArmourChanged(float oldArmour, float newArmour)
        {
            if(!isLocalPlayer) return;
            _lastArmourChangeTime = Time.time;

            if(ArmourCanvasGroup.alpha == _idleAlpha && newArmour < oldArmour)
            {
                ArmourCanvasGroup.TweenCanvasGroupAlpha(1f, 0.2f)
                    .SetEase(EaseType.CubicInOut);
            }

            ArmourBar.fillAmount = newArmour / MaxArmour;
            ArmourText.text = $"<b>{newArmour:0}</b><color=#EEEEEE>/{MaxArmour:0}</color>";
        }
    }
}
