using Mirror;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// For some reason, Network Identities sync their transforms on start, even if there's no Network Transform. This is a problem for UI. This script reverts that.
    /// </summary>
    public class BlockNetIdentityFromChangingTransform : NetworkBehaviour
    {
        private Vector3 _initialPos;
        private Quaternion _initialRot;
        private Vector3 _initialScale;

        protected void Awake()
        {
            _initialPos = transform.position;
            _initialRot = transform.rotation;
            _initialScale = transform.localScale;
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            transform.position = _initialPos;
            transform.rotation = _initialRot;
            transform.localScale = _initialScale;
        }
    }
}
