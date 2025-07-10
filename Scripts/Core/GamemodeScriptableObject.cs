using UnityEngine;

namespace Core
{
    [CreateAssetMenu(fileName = "Gamemode", menuName = "Gamemode")]
    public class GamemodeScriptableObject : ScriptableObject
    {
        public string Name;
        public string Description;
    }
}
