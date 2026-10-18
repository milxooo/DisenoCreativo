using UnityEditor;
using UnityEngine;

namespace RobaColores.Map.Editor
{
    public static class MapSetup
    {
        const string PalettePath = "Assets/Map/ScriptableObjects/MapPalette.asset";
        const string CameraPath = "Assets/Map/Prefabs/IsoPreviewCamera.prefab";

        [MenuItem("Roba Colores/Fase 1 - Crear paleta y camara")]
        public static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<MapPalette>(PalettePath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<MapPalette>(), PalettePath);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(CameraPath) == null)
            {
                var go = new GameObject("IsoPreviewCamera");
                var cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 12f;
                cam.nearClipPlane = -50f;
                cam.farClipPlane = 200f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.247f, 0.757f, 0.788f);
                go.transform.SetPositionAndRotation(new Vector3(-20f, 20f, -20f), Quaternion.Euler(30f, 45f, 0f));
                PrefabUtility.SaveAsPrefabAsset(go, CameraPath);
                Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
