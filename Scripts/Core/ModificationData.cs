using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    [Serializable]
    public class ModificationCategory
    {
        public string Name;
        
        [Tooltip("The currently active attachment index. -1 for none")]
        public int SelectedIndex;
        
        [Tooltip("Whether to use the first attachment as the default if none is selected")]
        public bool UseFirstAsDefault;
        
        public List<GameObject> Attachments;
    }
    
    public class ModificationData : MonoBehaviour
    {
        public string Name;
    }
}
