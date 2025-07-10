using UnityEngine;
using NetworkManager = Mirror.NetworkManager;

namespace Utils
{
    public class AutoHost : MonoBehaviour
    {
        private void Update()
        {
            if(Input.GetKeyDown(KeyCode.C))
                NetworkManager.singleton.StartClient();
            
            if(Input.GetKeyDown(KeyCode.H))
                NetworkManager.singleton.StartHost();
        }
    }
}
