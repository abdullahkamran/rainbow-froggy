using System;
using System.Collections;
using UnityEngine;
using RainbowFroggy.Core;

namespace RainbowFroggy.View
{
    // Frog sprite with jump, sink, and waterfall-ride animations.
    // All animations are coroutine-driven; IsAnimating is true while one runs.
    // IsInputBlocking is true only during the sink and ride-off animations (AC5).
    // LateUpdate applies an idle bob when no animation is active (AC6).
    public sealed class FrogView : MonoBehaviour
    {
        // Jump tween duration — stays in the 0.15–0.2 s band (AC1).
        private const float JumpDuration  = 0.175f;
        private const float ArcHeight     = 0.8f;
        private const float SinkDuration  = 0.4f;
        private const float RideDuration  = 0.3f;
        private const float FlashDuration = 0.18f;  // white-flash on color change
        private const float BobFreq       = 3.0f;
        private const float BobAmp        = 0.06f;

        private SpriteRenderer _sr;             // circle body renderer
        private Vector3        _anchor;
        private Coroutine      _activeCoroutine;
        private bool           _isInputBlocking;
        private Vector3        _originalScale;

        public bool IsAnimating     => _activeCoroutine != null;
        public bool IsInputBlocking => _isInputBlocking;

        private void Awake()
        {
            var mat = new Material(Shader.Find("Sprites/Default"));

            // White outline ring behind body.
            var outline = new GameObject("Outline");
            outline.transform.SetParent(transform, false);
            outline.transform.localScale = Vector3.one * 1.22f;
            var osr = outline.AddComponent<SpriteRenderer>();
            osr.sprite       = MakeCircleSprite(32);
            osr.color        = Color.white;
            osr.material     = mat;
            osr.sortingOrder = 4;

            // Circle body — _sr targets this for color / alpha operations.
            _sr              = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite       = MakeCircleSprite(32);
            _sr.material     = mat;
            _sr.sortingOrder = 5;

            SpawnEye(mat, new Vector3(-0.27f, 0.28f, 0f));
            SpawnEye(mat, new Vector3( 0.27f, 0.28f, 0f));

            transform.localScale = new Vector3(0.6f, 0.6f, 1f);
            _originalScale       = transform.localScale;
        }

        private void SpawnEye(Material mat, Vector3 localPos)
        {
            var eye = new GameObject("Eye");
            eye.transform.SetParent(transform, false);
            eye.transform.localPosition = localPos;
            eye.transform.localScale    = Vector3.one * 0.38f;

            var sr = eye.AddComponent<SpriteRenderer>();
            sr.sprite       = MakeCircleSprite(16);
            sr.color        = Color.white;
            sr.material     = mat;
            sr.sortingOrder = 6;

            var pupil = new GameObject("Pupil");
            pupil.transform.SetParent(eye.transform, false);
            pupil.transform.localPosition = new Vector3(0.12f, -0.08f, 0f);
            pupil.transform.localScale    = Vector3.one * 0.45f;

            var psr = pupil.AddComponent<SpriteRenderer>();
            psr.sprite       = MakeCircleSprite(8);
            psr.color        = Color.black;
            psr.material     = mat;
            psr.sortingOrder = 7;
        }

        // Idle bob: oscillate gently around the anchor while waiting for input (AC6).
        private void LateUpdate()
        {
            if (IsAnimating) return;
            transform.position = _anchor +
                new Vector3(0f, Mathf.Sin(Time.time * BobFreq) * BobAmp, 0f);
        }

        // Set the logical rest position the frog rides on its current pad.
        // Called by GameBootstrap.SyncAllPads each frame when not animating.
        public void SetAnchor(Vector3 pos) => _anchor = pos;

        public void SetColor(PadColor c) => _sr.color = ColorPalette.For(c);

        // Flash body white → newColor; fires as a background coroutine after landing (AC2).
        public void FlashColor(PadColor newColor)
        {
            StartCoroutine(FlashRoutine(newColor));
        }

        // Stop any running animation and restore the frog to its resting visual state.
        public void Reset()
        {
            if (_activeCoroutine != null)
            {
                StopCoroutine(_activeCoroutine);
                _activeCoroutine = null;
            }
            _isInputBlocking     = false;
            transform.localScale = _originalScale;
            var c                = _sr.color;
            c.a                  = 1f;
            _sr.color            = c;
        }

        // Animate the frog from its current world position to the target pad.
        // onComplete fires once the frog reaches the pad (AC2: color update goes there).
        public void JumpTo(Transform targetPad, Action onComplete)
        {
            if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
            _isInputBlocking = false;
            _activeCoroutine = StartCoroutine(JumpRoutine(targetPad, onComplete));
        }

        // Scale-down + alpha-fade for a wrong-pad landing (AC3).
        public void Sink(Action onComplete)
        {
            if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
            _isInputBlocking = true;
            _activeCoroutine = StartCoroutine(SinkRoutine(onComplete));
        }

        // Translate the frog downward at the field's scroll speed for RideDuration
        // seconds, then call onComplete (AC4).
        public void RideDown(float worldUnitsPerSec, Action onComplete)
        {
            if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
            _isInputBlocking = true;
            _activeCoroutine = StartCoroutine(RideDownRoutine(worldUnitsPerSec, onComplete));
        }

        // ------------------------------------------------------------------ //

        private IEnumerator JumpRoutine(Transform targetPad, Action onComplete)
        {
            var   startPos = transform.position;
            float elapsed  = 0f;

            while (elapsed < JumpDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / JumpDuration);

                // Re-read target each frame so landing tracks the scrolling pad.
                Vector3 targetPos = targetPad != null
                    ? targetPad.position + new Vector3(0f, 0.3f, 0f)
                    : transform.position;

                float arc          = 4f * ArcHeight * t * (1f - t);
                transform.position = Vector3.Lerp(startPos, targetPos, t)
                                     + new Vector3(0f, arc, 0f);
                yield return null;
            }

            if (targetPad != null)
                transform.position = targetPad.position + new Vector3(0f, 0.3f, 0f);
            _activeCoroutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator FlashRoutine(PadColor newColor)
        {
            Color startColor = _sr.color;
            Color endColor   = ColorPalette.For(newColor);
            float half       = FlashDuration * 0.5f;
            float elapsed    = 0f;

            while (elapsed < half)
            {
                elapsed   += Time.deltaTime;
                _sr.color  = Color.Lerp(startColor, Color.white, Mathf.Clamp01(elapsed / half));
                yield return null;
            }
            _sr.color = Color.white;
            elapsed = 0f;

            while (elapsed < half)
            {
                elapsed   += Time.deltaTime;
                _sr.color  = Color.Lerp(Color.white, endColor, Mathf.Clamp01(elapsed / half));
                yield return null;
            }
            _sr.color = endColor;
        }

        private IEnumerator SinkRoutine(Action onComplete)
        {
            var   startScale = transform.localScale;
            Color startColor = _sr.color;
            float elapsed    = 0f;

            while (elapsed < SinkDuration)
            {
                elapsed += Time.deltaTime;
                float t              = Mathf.Clamp01(elapsed / SinkDuration);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                Color c              = _sr.color;
                c.a                  = Mathf.Lerp(startColor.a, 0f, t);
                _sr.color            = c;
                yield return null;
            }

            _activeCoroutine = null;
            _isInputBlocking = false;
            onComplete?.Invoke();
        }

        private IEnumerator RideDownRoutine(float worldUnitsPerSec, Action onComplete)
        {
            float elapsed = 0f;

            while (elapsed < RideDuration)
            {
                elapsed            += Time.deltaTime;
                transform.position +=
                    new Vector3(0f, -worldUnitsPerSec * Time.deltaTime, 0f);
                yield return null;
            }

            _activeCoroutine = null;
            _isInputBlocking = false;
            onComplete?.Invoke();
        }

        internal static Sprite MakeCircleSprite(int radius)
        {
            int size = radius * 2;
            var tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            float cx = radius - 0.5f, cy = radius - 0.5f;
            float r2 = radius * radius;
            for (int i = 0; i < pixels.Length; i++)
            {
                float dx = (i % size) - cx;
                float dy = (i / size) - cy;
                pixels[i] = dx * dx + dy * dy <= r2
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size);
        }
    }
}
