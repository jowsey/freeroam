using System.Collections.Generic;
using Player;
using UnityEngine;

namespace UI
{
    public class PlayerOverlayManager : MonoBehaviour
    {
        public static PlayerOverlayManager Instance { get; private set; }

        [SerializeField]
        private PlayerOverlay _overlayPrefab;

        [SerializeField]
        private List<PlayerOverlay> _overlays;

        private Camera _camera;

        private void Start()
        {
            if(Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;

            _camera = Camera.main;
        }

        private void LateUpdate()
        {
            foreach (var overlay in _overlays)
            {
                var screenPos = _camera.WorldToScreenPoint(overlay.LinkedPlayer.transform.position + Vector3.up * 2.3f);
                overlay.transform.position = screenPos;

                // dont render if the player is behind the camera
                overlay.gameObject.SetActive(screenPos.z >= 0);

                var viewportPoint = _camera.ScreenToViewportPoint(screenPos);

                // if the player is within the horizontal borders of the viewport, fade out alpha
                var viewportXAlphaMultiplier = viewportPoint.x <= 0.3f ? Mathf.InverseLerp(0, 0.3f, viewportPoint.x)
                    : viewportPoint.x >= 0.7f ? 1 - Mathf.InverseLerp(0.7f, 1, viewportPoint.x) : 1;

                // if the player is more than 25 units away, fade out alpha until 50 units away
                var distance = Vector3.Distance(overlay.LinkedPlayer.transform.position, PlayerController.LocalPlayer.transform.position);
                var distanceAlphaMultiplier = 1 - Mathf.InverseLerp(25, 50, distance);

                // lerp to 3/4 scale over same distance
                overlay.transform.localScale = Vector3.one * Mathf.Lerp(0.75f, 1f, distanceAlphaMultiplier);

                // multply alpha by 0.25 if the player is behind a wall
                var hiddenAlphaMultiplier = Physics.Linecast(
                    overlay.LinkedPlayer.transform.position + Vector3.up * 2.3f,
                    _camera.transform.position,
                    out var hit
                ) && hit.transform != overlay.LinkedPlayer.transform
                    ? 0.25f
                    : 1f;

                overlay.CanvasGroup.alpha = 1 * viewportXAlphaMultiplier * distanceAlphaMultiplier * hiddenAlphaMultiplier;
            }
        }

        public void AddOverlay(PlayerController player)
        {
            var overlay = Instantiate(_overlayPrefab, transform);
            overlay.LinkedPlayer = player;
            _overlays.Add(overlay);
        }

        public void RemoveOverlay(PlayerController player)
        {
            var overlay = _overlays.Find(overlay => overlay.LinkedPlayer == player);
            if(overlay == null) return;

            _overlays.Remove(overlay);
            if(overlay.gameObject != null) Destroy(overlay.gameObject);
        }
    }
}
