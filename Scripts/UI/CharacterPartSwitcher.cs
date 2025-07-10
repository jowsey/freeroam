using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UI
{
    public class CharacterPartSwitcher : MonoBehaviour
    {
        public int Index;
        public int MaxIndex;
        public bool AllowNone;

        public Button PreviousButton;
        public TextMeshProUGUI IndexText;
        public Button NextButton;

        public UnityEvent<int> OnIndexChange = new();

        private void Start()
        {
            OnIndexChange.AddListener(index =>
            {
                IndexText.text = $"{index + 1}/{MaxIndex + 1}";
            });
            
            OnIndexChange.Invoke(Index);
        }

        public void CyclePrevious()
        {
            Index--;
            if(Index < (AllowNone ? -1 : 0)) Index = MaxIndex;
            
            OnIndexChange.Invoke(Index);
        }

        public void CycleNext()
        {
            Index++;

            if(Index > MaxIndex) Index = AllowNone ? -1 : 0;
            OnIndexChange.Invoke(Index);
        }
    }
}
