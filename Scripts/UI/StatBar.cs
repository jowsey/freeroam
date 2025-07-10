using ElRaccoone.Tweens;
using ElRaccoone.Tweens.Core;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class StatBar : MonoBehaviour
    {
        public Image BarImage;
        public Image NewBarImage;

        private void Start()
        {
            BarImage.fillAmount = 0.5f;
            NewBarImage.fillAmount = 0.5f;
        }

        public void SetNewValue(float fillAmount)
        {
            if(fillAmount > BarImage.fillAmount)
            {
                NewBarImage.color = Color.green;

                NewBarImage.TweenImageFillAmount(fillAmount, 0.1f)
                    .SetEase(EaseType.CubicInOut);
            }
            else
            {
                NewBarImage.color = Color.white;
                BarImage.color = Color.red;
                
                BarImage.TweenImageFillAmount(fillAmount, 0.1f)
                    .SetEase(EaseType.CubicInOut);
            }
        }

        public void SetCurrentValue(float fillAmount)
        {
            BarImage.TweenImageFillAmount(fillAmount, 0.1f)
                .SetEase(EaseType.CubicInOut);
        }
    }
}
