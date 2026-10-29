using System.IO;
using UnityEngine;

namespace RobaColores.Game.EditorTools
{
    // Herramienta de pruebas (solo editor, en modo Play): renderiza el menu a una resolucion arbitraria y la guarda como PNG
    // para comprobar que la composicion se adapta. Uso en dos pasos separados por unos fotogramas: Begin y luego Finish.
    public static class MenuResolutionCapture
    {
        static Canvas canvas;
        static Camera cam;
        static RenderTexture rt;
        static RenderMode previousMode;
        static Camera previousCamera;

        public static string Begin(int width, int height)
        {
            var go = GameObject.Find("MenuCanvas");
            if (go == null) return "No hay MenuCanvas (abre la escena Menu en modo Play).";
            canvas = go.GetComponent<Canvas>();
            previousMode = canvas.renderMode;
            previousCamera = canvas.worldCamera;

            var camGo = new GameObject("CamaraPruebaResolucion");
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            cam.cullingMask = 1 << 5;                  // capa UI
            rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            return "Preparado " + width + "x" + height;
        }

        public static string Finish(string path)
        {
            if (cam == null || rt == null) return "Llama primero a Begin.";
            Canvas.ForceUpdateCanvases();
            cam.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = previous;

            Object.Destroy(tex);
            canvas.renderMode = previousMode;
            canvas.worldCamera = previousCamera;
            cam.targetTexture = null;
            Object.Destroy(cam.gameObject);
            rt.Release();
            Object.Destroy(rt);
            cam = null; rt = null;
            return "Guardado " + path;
        }
    }
}
