using TMPro;
using UnityEngine;

namespace UI.MainMenu
{
    [ExecuteAlways]
    public class VersionText : MonoBehaviour
    {
        private TextMeshProUGUI _text;
        
        [ContextMenu("Update")]
        private void OnEnable()
        {
            _text = GetComponent<TextMeshProUGUI>();
            _text.text = $"alpha - v{Application.version}";
        }
    }
}
