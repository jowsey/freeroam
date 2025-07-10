using Mirror;
using UnityEngine.Events;

namespace Core
{
    public class NetworkManager : Mirror.NetworkManager
    {
        public UnityEvent<NetworkConnectionToClient> OnPlayerJoins = new();
        public UnityEvent<NetworkConnectionToClient> OnPlayerLeaves = new();
        
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            base.OnServerAddPlayer(conn);
            
            OnPlayerJoins.Invoke(conn);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            OnPlayerLeaves.Invoke(conn);
            
            base.OnServerDisconnect(conn);
        }
    }
}
