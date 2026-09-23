using System;
using System.Collections;
using UnityEngine;

namespace RainbowFroggy.View
{
    // Slide-up bottom-sheet panel.  Starts hidden below the screen; Open()
    // slides it into view, Close() slides it back down.
    // The sheet's parent GameObject starts inactive; Open activates it first.
    public sealed class BottomSheet : MonoBehaviour
    {
        private const float SlideDuration = 0.25f;

        private RectTransform _rt;
        private float         _closedY; // anchoredPosition.y when hidden
        private float         _openY;   // anchoredPosition.y when shown
        private Coroutine     _active;

        // Called by GameBootstrap after the RectTransform is set up.
        public void Init(RectTransform rt, float closedY, float openY)
        {
            _rt      = rt;
            _closedY = closedY;
            _openY   = openY;

            var pos  = _rt.anchoredPosition;
            pos.y                = _closedY;
            _rt.anchoredPosition = pos;
            gameObject.SetActive(false);
        }

        public void Open()
        {
            if (_active != null) StopCoroutine(_active);
            gameObject.SetActive(true);
            _active = StartCoroutine(SlideTo(_openY, null));
        }

        public void Close()
        {
            if (_active != null) StopCoroutine(_active);
            _active = StartCoroutine(SlideTo(_closedY, () => gameObject.SetActive(false)));
        }

        private IEnumerator SlideTo(float targetY, Action onComplete)
        {
            float startY  = _rt.anchoredPosition.y;
            float elapsed = 0f;

            while (elapsed < SlideDuration)
            {
                elapsed += Time.deltaTime;
                var pos  = _rt.anchoredPosition;
                pos.y                = Mathf.Lerp(startY, targetY,
                                          Mathf.Clamp01(elapsed / SlideDuration));
                _rt.anchoredPosition = pos;
                yield return null;
            }

            var final  = _rt.anchoredPosition;
            final.y                = targetY;
            _rt.anchoredPosition   = final;
            _active                = null;
            onComplete?.Invoke();
        }
    }
}
