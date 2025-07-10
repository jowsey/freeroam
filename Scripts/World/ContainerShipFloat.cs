using Mirror;
using UnityEngine;

namespace World
{
    public class ContainerShipFloat : NetworkBehaviour
    {
        private float yOffset;
        private float initialY;

        public override void OnStartServer()
        {
            base.OnStartServer();
            initialY = transform.position.y;
        }

        private void FixedUpdate()
        {
            if(!isServer) return;
            yOffset = Mathf.Sin(Time.time) * 0.3f;
            
            transform.position = new Vector3(transform.position.x, initialY + yOffset, transform.position.z);
        }
    }
}
