using UnityEngine;

namespace RobaColores.Game.Presentation
{
    // Escala un "escenario" de tamano de diseno fijo para que quepa entero en su padre, sea cual sea la relacion de aspecto.
    // Asi la composicion del boceto se conserva en cualquier resolucion y nada se corta ni se superpone.
    public sealed class StageFitter : MonoBehaviour
    {
        public Vector2 design = new Vector2(1393f, 1080f);

        public static float ScaleFor(Vector2 parentSize, Vector2 design)
        {
            if (design.x <= 0f || design.y <= 0f) return 1f;
            return Mathf.Min(parentSize.x / design.x, parentSize.y / design.y);
        }

        void LateUpdate()
        {
            var parent = transform.parent as RectTransform;
            if (parent == null) return;
            float s = ScaleFor(parent.rect.size, design);
            if (s > 0.0001f) transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
