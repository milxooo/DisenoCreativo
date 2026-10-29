using System;
using System.Collections;
using UnityEngine;

namespace RobaColores.Game.Presentation
{
    // Animaciones de interfaz sin librerias externas. Usan tiempo sin escala para no depender de Time.timeScale.
    public static class UiAnim
    {
        public static float OutCubic(float t) { t = 1f - t; return 1f - t * t * t; }

        public static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            t -= 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        public static float InOutSine(float t) { return -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f; }

        // Ejecuta step(k) con k de 0 a 1 (con easing) durante "duration" segundos, tras un retraso opcional.
        public static IEnumerator Run(float duration, Action<float> step, Func<float, float> ease = null, float delay = 0f)
        {
            for (float d = 0f; d < delay; d += Time.unscaledDeltaTime) yield return null;
            if (duration <= 0f) { step(ease != null ? ease(1f) : 1f); yield break; }
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / duration);
                step(ease != null ? ease(k) : k);
                yield return null;
            }
            step(ease != null ? ease(1f) : 1f);
        }

        public static IEnumerator Fade(CanvasGroup group, float from, float to, float duration, float delay = 0f)
        {
            return Run(duration, k => { if (group != null) group.alpha = Mathf.Lerp(from, to, k); }, null, delay);
        }

        public static IEnumerator Slide(RectTransform rt, Vector2 from, Vector2 to, float duration, float delay = 0f, Func<float, float> ease = null)
        {
            return Run(duration, k => { if (rt != null) rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k); }, ease ?? OutCubic, delay);
        }

        public static IEnumerator Pop(RectTransform rt, float duration, float delay = 0f)
        {
            return Run(duration, k => { if (rt != null) rt.localScale = Vector3.one * Mathf.Max(0f, k); }, OutBack, delay);
        }
    }
}
