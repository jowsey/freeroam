using ElRaccoone.Tweens;
using ElRaccoone.Tweens.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI.MainMenu
{
    public class MenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public bool ShouldAnimate = true;
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            if(!ShouldAnimate) return;
            
            gameObject.TweenLocalScaleX(1.1f, 0.1f)
                .SetEase(EaseType.CubicInOut)
                .SetUseUnscaledTime(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            gameObject.TweenLocalScaleX(1f, 0.1f)
                .SetEase(EaseType.CubicInOut)
                .SetUseUnscaledTime(true);
        }
    }
}
