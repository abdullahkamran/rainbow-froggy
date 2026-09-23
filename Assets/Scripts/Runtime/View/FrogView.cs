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
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class FrogView : MonoBehaviour
    {
        // Jump tween duration — stays in the 0.15–0.2 s band (AC1).
        private const float JumpDuration = 0.175f;
        private const float ArcHeight    = 0.8f;   // world-unit peak of the parabola
        private const float SinkDuration = 0.4f;
        private const float RideDuration = 0.3f;
        private const float BobFreq      = 3.0f;   // rad/s
        private const float BobAmp       = 0.06f;  // world units

        private SpriteRenderer _sr;
        private Vector3        _anchor;            // rest position, updated by GameBootstrap
        private Coroutine      _activeCoroutine;
        private bool           _isInputBlocking;
        private Vector3        _originalScale;     // captured in Awake for Reset()

        // True while any animation coroutine is running.
        public bool IsAnimating => _activeCoroutine != null;

        // True only during sink and ride-off — callers must not accept input (AC5).
        public bool IsInputBlocking => _isInputBlocking;

        private void Awake()
        {
            _sr            = GetComponent<SpriteRenderer>();
            _originalScale = transform.localScale;
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

        // Stop any running animation and restore the frog to its resting visual
        // state.  Called by GameBootstrap when restarting the game in-place.
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

        // Animate the frog from its current world position to the target pad over
        // JumpDuration seconds using a parabolic arc.  The target position is
        // re-read each frame so the frog tracks a scrolling pad at any speed (AC7).
        // onComplete fires once the frog reaches the pad.
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
                // Fall back to the last known position if the pad is destroyed mid-flight.
                Vector3 targetPos = targetPad != null
                    ? targetPad.position + new Vector3(0f, 0.3f, 0f)
                    : transform.position;

                float arc          = 4f * ArcHeight * t * (1f - t);
                transform.position = Vector3.Lerp(startPos, targetPos, t)
                                     + new Vector3(0f, arc, 0f);
                yield return null;
            }

            // Snap to final resting position then fire the callback (AC2: color
            // update happens inside onComplete, not at tap time).
            if (targetPad != null)
                transform.position = targetPad.position + new Vector3(0f, 0.3f, 0f);
            _activeCoroutine = null;
            onComplete?.Invoke();
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
    }
}
