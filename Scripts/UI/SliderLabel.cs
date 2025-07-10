using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    [ExecuteAlways]
    public class SliderLabel : MonoBehaviour
    {
        [SerializeField]
        private Slider slider;

        [SerializeField]
        private TextMeshProUGUI label;

        [SerializeField]
        private string prefix;

        [SerializeField]
        private string suffix;

        [Range(0, 15)]
        [SerializeField]
        private int roundDecimalPlaces;

        // Start is called before the first frame update
        // private void OnValidate() => slider.onValueChanged.AddListener(val => label.text = $"{prefix}{val}{suffix}");

        private void Start() => OnValidate();

        private void OnValidate()
        {
            if(slider == null || label == null) return;

            slider.onValueChanged.AddListener(UpdateLabel);
            UpdateLabel(slider.value);
        }

        /// <summary>
        /// Update text based on slider value
        /// </summary>
        /// <param name="val">Value to use</param>
        private void UpdateLabel(float val) => label.text = $"{prefix}{(roundDecimalPlaces > 0 ? Math.Round(val, roundDecimalPlaces) : val).ToString(CultureInfo.CurrentCulture)}{suffix}";
    }
}
