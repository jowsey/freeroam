using UnityEngine;

namespace Utils
{
    public class ToggleTerrainCollider : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<TerrainCollider>().enabled = false;
            GetComponent<TerrainCollider>().enabled = true;
        }
    }
}
