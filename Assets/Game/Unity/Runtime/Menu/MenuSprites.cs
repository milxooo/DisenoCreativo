using UnityEngine;

namespace RobaColores.Game.Presentation
{
    // Sprites del menu generados por codigo: marco de doble linea (9-slice), circulo y pergamino con bordes quemados.
    // Asi el menu no depende de arte externo y se puede sustituir luego por sprites reales.
    public static class MenuSprites
    {
        static Sprite frame, circle, parchment;

        // Marco redondeado de doble linea (como los botones del boceto). Se tine con Image.color.
        public static Sprite Frame()
        {
            if (frame != null) return frame;
            const int n = 40;
            const float r = 9f;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    bool inCorner = (fx < r || fx > n - r) && (fy < r || fy > n - r);
                    float d;
                    if (inCorner)
                    {
                        float cx = Mathf.Clamp(fx, r, n - r), cy = Mathf.Clamp(fy, r, n - r);
                        d = r - Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy));
                    }
                    else d = Mathf.Min(Mathf.Min(fx, n - fx), Mathf.Min(fy, n - fy));

                    if (d < 0f) { px[y * n + x] = new Color32(0, 0, 0, 0); continue; }
                    byte shade = 255;
                    if (d < 2.2f) shade = 70;
                    else if (d >= 3.6f && d < 5.2f) shade = 105;
                    byte alpha = (byte)(255 * Mathf.Clamp01(d * 2f));
                    px[y * n + x] = new Color32(shade, shade, shade, alpha);
                }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            frame = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(13, 13, 13, 13));
            return frame;
        }

        public static Sprite Circle()
        {
            if (circle != null) return circle;
            const int n = 48;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    float a = Mathf.Clamp01(n / 2f - d);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return circle;
        }

        // Papel con manchas suaves y bordes irregulares quemados; fuera del borde es transparente.
        public static Sprite Parchment()
        {
            if (parchment != null) return parchment;
            const int w = 696, h = 540;
            var px = new Color32[w * h];
            var paper = new Color(0.92f, 0.867f, 0.71f);
            var burnt = new Color(0.20f, 0.12f, 0.06f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float n1 = Mathf.PerlinNoise(x * 0.011f + 7.3f, y * 0.011f + 3.1f);
                    float n2 = Mathf.PerlinNoise(x * 0.07f + 1.7f, y * 0.07f + 9.4f);
                    float n3 = Mathf.PerlinNoise(x * 0.25f + 4.2f, y * 0.25f + 2.8f);
                    float edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y)) + (n1 - 0.5f) * 44f + (n2 - 0.5f) * 12f;
                    if (edge < 0f) { px[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                    float burn = Mathf.Clamp01(1f - edge / 30f);
                    burn = burn * burn * (3f - 2f * burn);
                    float shade = 0.94f + 0.06f * n2 - 0.04f * n3;
                    Color c = Color.Lerp(paper * shade, burnt, Mathf.Clamp01(burn * 1.15f));
                    c = Color.Lerp(c, new Color(0.55f, 0.38f, 0.2f), Mathf.Clamp01((1f - edge / 90f) * 0.18f));
                    px[y * w + x] = new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), (byte)(255 * Mathf.Clamp01(edge * 0.8f)));
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            parchment = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            return parchment;
        }
    }
}
