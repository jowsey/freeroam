using UnityEngine;

namespace Core
{
    [CreateAssetMenu(fileName = "Map", menuName = "Map")]
    public class MapScriptableObject : ScriptableObject
    {
        public string MapName;
        public string SceneName;
    }
}
