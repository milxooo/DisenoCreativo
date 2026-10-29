using System;
using UnityEngine;
using UnityEngine.UI;

namespace RobaColores.Game.Presentation
{
    // Presentacion de los dos dados del juego: orden inicial (1 a 6) y movimiento (1 a 4, pierde turno, penalizacion).
    // Es una introduccion visual: usa su propio azar y NO modifica el estado de la partida ni el orden de los jugadores.
    public sealed class DiceShowcase : MonoBehaviour
    {
        public DiceView OrderDie { get; private set; }
        public DiceView MoveDie { get; private set; }
        public bool AutoRoll = true;
        public float AutoRollSeconds = 7f;

        Text orderResult, moveResult;
        float idle;

        public bool IsRolling { get { return OrderDie.IsRolling || MoveDie.IsRolling; } }

        public static DiceShowcase Create(RectTransform parent, MenuArt art, float dieSize, bool captions)
        {
            var go = new GameObject("PresentacionDados", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var show = go.AddComponent<DiceShowcase>();
            show.Build((RectTransform)go.transform, art, dieSize, captions);
            return show;
        }

        void Build(RectTransform root, MenuArt art, float dieSize, bool captions)
        {
            float colW = captions ? 172f : dieSize + 40f;
            root.anchorMin = root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 0f);
            root.sizeDelta = new Vector2(colW * 2f, captions ? 430f : dieSize + 40f);

            var orderFaces = art != null ? art.orderDiceFaces : null;
            var moveFaces = art != null ? art.moveDiceFaces : null;
            float dieY = captions ? 330f : root.sizeDelta.y / 2f;

            OrderDie = DiceView.Create(root, DiceView.Kind.Order, dieSize, orderFaces);
            MoveDie = DiceView.Create(root, DiceView.Kind.Movement, dieSize, moveFaces);
            PlaceCenter(OrderDie.transform as RectTransform, colW * 0.5f, dieY);
            PlaceCenter(MoveDie.transform as RectTransform, colW * 1.5f, dieY);
            MakeClickable(OrderDie);
            MakeClickable(MoveDie);

            if (captions)
            {
                Caption(root, "DADO DE ORDEN", 0, colW, 232, 25, UiKit.Ink);
                Caption(root, "Caras del 1 al 6.\nDecide quién empieza.", 0, colW, 156, 18, UiKit.Wood);
                orderResult = Caption(root, "", 0, colW, 112, 26, new Color32(0x9C, 0x2B, 0x2B, 255));
                Caption(root, "DADO DE\nMOVIMIENTO", colW, colW, 212, 25, UiKit.Ink);
                Caption(root, "Del 1 al 4: avanzas.\nOtras dos caras\nte castigan.", colW, colW, 120, 18, UiKit.Wood);
                moveResult = Caption(root, "", colW, colW, 46, 26, new Color32(0x9C, 0x2B, 0x2B, 255));
            }
            OrderDie.Finished += OnFinished;
            MoveDie.Finished += OnFinished;
        }

        static void PlaceCenter(RectTransform rt, float x, float y)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
        }

        static Text Caption(RectTransform root, string text, float x, float w, float y, int fontSize, Color color)
        {
            var t = UiKit.Label(root, text, fontSize, TextAnchor.UpperCenter, color);
            UiKit.Place(t.rectTransform, 0, 0, 0, 0, x, y - 80, x + w, y + 30);
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.alignment = TextAnchor.UpperCenter;
            return t;
        }

        void MakeClickable(DiceView die)
        {
            var body = die.transform.GetChild(0);
            var button = body.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = body.GetComponent<Image>();
            button.onClick.AddListener(() => { if (!die.IsRolling) { idle = 0f; die.Roll(); } });
        }

        void OnFinished(DiceView die, int face)
        {
            if (die == OrderDie && orderResult != null) orderResult.text = "Salió el " + (face + 1);
            if (die == MoveDie && moveResult != null)
                moveResult.text = face < 4 ? "Avanzas " + (face + 1) : face == 4 ? "Pierdes un turno" : "Penalización de tiempo";
        }

        public void RollAll(Action done = null)
        {
            idle = 0f;
            OrderDie.Roll();
            if (done != null) MoveDie.Finished += OnceDone(done);
            MoveDie.Roll();
        }

        Action<DiceView, int> OnceDone(Action done)
        {
            Action<DiceView, int> handler = null;
            handler = (d, f) => { MoveDie.Finished -= handler; done(); };
            return handler;
        }

        void Update()
        {
            if (!AutoRoll) return;
            if (IsRolling) { idle = 0f; return; }
            idle += Time.unscaledDeltaTime;
            if (idle >= AutoRollSeconds) RollAll();
        }
    }
}
