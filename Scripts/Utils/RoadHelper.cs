using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Utils
{
    [Obsolete]
    public class RoadHelper : MonoBehaviour
    {
        public List<GameObject> SidewalkPieces;

        public GameObject DefaultRoadPiece;

        public GameObject CrossingRoadPiece;
        public GameObject CrossingSidewalkPiece;

#if UNITY_EDITOR
        /// <summary>
        /// Snap all selected objects to grid
        /// </summary>
        [ContextMenu("Snap to grid")]
        public void SnapToGrid()
        {
            var selection = Selection.gameObjects;

            // snap to nearest .25
            foreach (var obj in selection)
            {
                var position = obj.transform.position;
                position.x = Mathf.Round(position.x * 4f) / 4f;
                position.z = Mathf.Round(position.z * 4f) / 4f;
                obj.transform.position = position;
            }
        }

        /// <summary>
        /// Replace each selected object with a random sidewalk object
        /// </summary>
        [ContextMenu("Randomize sidewalk pieces")]
        public void RandomSidewalkPieces()
        {
            var selection = Selection.transforms;

            foreach (var obj in selection)
            {
                if(!obj.name.ToLower().Contains("sidewalk")) continue;

                var range = Random.Range(0f, 1f);
                if(range >= 0.2f) continue;

                var position = obj.position;
                var rotation = obj.rotation;
                var scale = obj.localScale;
                var parent = obj.parent;
                var siblingIndex = obj.GetSiblingIndex();

                var randomIndex = Random.Range(0, SidewalkPieces.Count);
                var randomSidewalkPiece = SidewalkPieces[randomIndex];

                DestroyImmediate(obj.gameObject);

                var newSidewalkPiece = Instantiate(randomSidewalkPiece, position, rotation).transform;
                newSidewalkPiece.localScale = scale;
                newSidewalkPiece.parent = parent;
                newSidewalkPiece.SetSiblingIndex(siblingIndex);
            }
        }

        [ContextMenu("Make all default sidewalk")]
        public void MakeAllDefaultSidewalk()
        {
            var selection = Selection.transforms;

            foreach (var obj in selection)
            {
                if(!obj.name.ToLower().Contains("sidewalk")) continue;

                var position = obj.position;
                var rotation = obj.rotation;
                var scale = obj.localScale;
                var parent = obj.parent;
                var siblingIndex = obj.GetSiblingIndex();

                var sidewalkPiece = SidewalkPieces[0];

                DestroyImmediate(obj.gameObject);

                var newSidewalkPiece = Instantiate(sidewalkPiece, position, rotation).transform;
                newSidewalkPiece.localScale = scale;
                newSidewalkPiece.parent = parent;
                newSidewalkPiece.SetSiblingIndex(siblingIndex);
            }
        }

        // i got sick of acidentally pressing default sidewalk
        [ContextMenu("-----")]
        public void SafetySpacer1()
        {
        }

        [ContextMenu("------")]
        public void SafetySpacer2()
        {
        }
#endif
    }
}
