using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RobaColores.Game.Presentation
{
    // Dado visual 2D (provisional) para la presentacion del menu. Solo es decoracion: no toca reglas ni estado de partida.
    // Las caras se dibujan por codigo; para usar arte propio basta con pasar sprites (MenuArt.orderDiceFaces / moveDiceFaces).
    public sealed class DiceView : MonoBehaviour
    {
        public enum Kind { Order, Movement }

        static readonly Color Cloth = new Color32(0xF4, 0xEF, 0xE0, 255);
        static readonly Color Wood = new Color32(0x6B, 0x44, 0x23, 255);
        static readonly Color Red = new Color32(0xD8, 0x36, 0x3A, 255);

        Kind kind;
        float size;
        RectTransform body;
        Image bodyImage;
        readonly GameObject[] faces = new GameObject[6];
        Sprite[] overrides;
        bool rolling;

        public event Action<DiceView, int> Finished;

        public bool IsRolling { get { return rolling; } }
        public int CurrentFace { get; private set; }
        public Kind DieKind { get { return kind; } }

        public static DiceView Create(Transform parent, Kind kind, float size, Sprite[] faceOverrides)
        {
            var go = new GameObject(kind == Kind.Order ? "DadoOrden" : "DadoMovimiento", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(size, size);
            var view = go.AddComponent<DiceView>();
            view.Build(kind, size, faceOverrides);
            return view;
        }

        void Build(Kind dieKind, float dieSize, Sprite[] faceOverrides)
        {
            kind = dieKind; size = dieSize; overrides = faceOverrides;
            body = UiKit.Rect(transform, "Cuerpo");
            body.anchorMin = body.anchorMax = new Vector2(0.5f, 0.5f);
            body.sizeDelta = new Vector2(size, size);
            body.anchoredPosition = Vector2.zero;
            bodyImage = body.gameObject.AddComponent<Image>();
            bodyImage.sprite = MenuSprites.Frame();
            bodyImage.type = Image.Type.Sliced;
            bodyImage.color = kind == Kind.Order ? Cloth : Wood;
            for (int i = 0; i < 6; i++) BuildFace(i);
            Show(UnityEngine.Random.Range(0, 6));
        }

        void BuildFace(int i)
        {
            var root = UiKit.Rect(body, "Cara" + (i + 1));
            UiKit.Stretch(root);
            faces[i] = root.gameObject;
            faces[i].SetActive(false);

            var overrideSprites = overrides;
            if (overrideSprites != null && i < overrideSprites.Length && overrideSprites[i] != null)
            {
                var img = root.gameObject.AddComponent<Image>();
                img.sprite = overrideSprites[i];
                img.preserveAspect = true;
                UiKit.Place(root, 0, 0, 1, 1, 8, 8, -8, -8);
                return;
            }
            if (kind == Kind.Order) Pips(root, i + 1);
            else MovementFace(root, i);
        }

        void Pips(RectTransform root, int n)
        {
            Vector2[] p;
            switch (n)
            {
                case 1: p = new[] { V(.5f, .5f) }; break;
                case 2: p = new[] { V(.28f, .72f), V(.72f, .28f) }; break;
                case 3: p = new[] { V(.26f, .74f), V(.5f, .5f), V(.74f, .26f) }; break;
                case 4: p = new[] { V(.28f, .72f), V(.72f, .72f), V(.28f, .28f), V(.72f, .28f) }; break;
                case 5: p = new[] { V(.28f, .72f), V(.72f, .72f), V(.5f, .5f), V(.28f, .28f), V(.72f, .28f) }; break;
                default: p = new[] { V(.3f, .76f), V(.3f, .5f), V(.3f, .24f), V(.7f, .76f), V(.7f, .5f), V(.7f, .24f) }; break;
            }
            foreach (var pos in p) Dot(root, pos, size * 0.19f, UiKit.Ink);
        }

        void MovementFace(RectTransform root, int i)
        {
            if (i < 4)
            {
                var t = UiKit.Label(root, (i + 1).ToString(), Mathf.RoundToInt(size * 0.66f), TextAnchor.MiddleCenter, Cloth);
                UiKit.Stretch(t.rectTransform);
            }
            else if (i == 4)
            {
                // Perder turno: aspa roja.
                for (int k = 0; k < 2; k++)
                {
                    var bar = UiKit.Panel(root, "Aspa", Red);
                    Center(bar.rectTransform, new Vector2(size * 0.66f, size * 0.13f));
                    bar.rectTransform.localRotation = Quaternion.Euler(0, 0, k == 0 ? 45f : -45f);
                }
            }
            else
            {
                // Penalizacion de tiempo: reloj.
                var ring = UiKit.Panel(root, "Reloj", Cloth);
                ring.sprite = MenuSprites.Circle();
                Center(ring.rectTransform, new Vector2(size * 0.68f, size * 0.68f));
                var face = UiKit.Panel(root, "Esfera", Wood);
                face.sprite = MenuSprites.Circle();
                Center(face.rectTransform, new Vector2(size * 0.54f, size * 0.54f));
                Hand(root, size * 0.05f, size * 0.22f, 0f);
                Hand(root, size * 0.05f, size * 0.15f, 90f);
            }
        }

        void Hand(RectTransform root, float w, float h, float angle)
        {
            var hand = UiKit.Panel(root, "Aguja", Cloth);
            hand.rectTransform.anchorMin = hand.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            hand.rectTransform.pivot = new Vector2(0.5f, 0f);
            hand.rectTransform.sizeDelta = new Vector2(w, h);
            hand.rectTransform.anchoredPosition = Vector2.zero;
            hand.rectTransform.localRotation = Quaternion.Euler(0, 0, -angle);
        }

        static Vector2 V(float x, float y) { return new Vector2(x, y); }

        void Dot(RectTransform root, Vector2 pos, float diameter, Color color)
        {
            var dot = UiKit.Panel(root, "Punto", color);
            dot.sprite = MenuSprites.Circle();
            var rt = dot.rectTransform;
            rt.anchorMin = rt.anchorMax = pos;
            rt.sizeDelta = new Vector2(diameter, diameter);
            rt.anchoredPosition = Vector2.zero;
        }

        static void Center(RectTransform rt, Vector2 sizeDelta)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = Vector2.zero;
        }

        public void Show(int face)
        {
            CurrentFace = Mathf.Clamp(face, 0, 5);
            for (int i = 0; i < faces.Length; i++) faces[i].SetActive(i == CurrentFace);
        }

        // Lanza el dado: gira, rebota y se asienta en la cara indicada (o una al azar). No bloquea la interfaz.
        public Coroutine Roll(int finalFace = -1)
        {
            if (rolling) return null;
            return StartCoroutine(RollRoutine(finalFace < 0 ? UnityEngine.Random.Range(0, 6) : finalFace));
        }

        IEnumerator RollRoutine(int finalFace)
        {
            rolling = true;
            const float duration = 1.15f;
            float spin = (UnityEngine.Random.value < 0.5f ? -1f : 1f) * (540f + UnityEngine.Random.Range(0f, 360f));
            float nextSwap = 0f;
            int last = CurrentFace;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                nextSwap -= Time.unscaledDeltaTime;
                if (nextSwap <= 0f)
                {
                    int f;
                    do { f = UnityEngine.Random.Range(0, 6); } while (f == last);
                    last = f;
                    Show(f);
                    nextSwap = Mathf.Lerp(0.05f, 0.26f, k * k);
                }
                body.localEulerAngles = new Vector3(0f, 0f, spin * (1f - UiAnim.OutCubic(k)));
                body.anchoredPosition = new Vector2(0f, Mathf.Sin(Mathf.Min(1f, k * 1.4f) * Mathf.PI) * size * 0.22f * (1f - k));
                body.localScale = Vector3.one * (1f + 0.16f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            Show(finalFace);
            body.localEulerAngles = Vector3.zero;
            body.anchoredPosition = Vector2.zero;
            yield return UiAnim.Run(0.22f, k => { if (body != null) body.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(k * Mathf.PI)); });
            body.localScale = Vector3.one;
            rolling = false;
            var handler = Finished;
            if (handler != null) handler(this, finalFace);
        }

        void OnDisable()
        {
            // Si se oculta a mitad de un lanzamiento, deja el dado en un estado coherente.
            if (rolling && body != null)
            {
                rolling = false;
                body.localEulerAngles = Vector3.zero;
                body.anchoredPosition = Vector2.zero;
                body.localScale = Vector3.one;
            }
        }
    }
}
