using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RobaColores.Game.Presentation;

namespace RobaColores.Game.EditorTools
{
    // Crea las escenas Menu y Partida y las registra en Build Settings (necesario para cargarlas por nombre).
    public static class GameSceneBuilder
    {
        const string MenuPath = "Assets/Game/Scenes/Menu.unity";
        const string GamePath = "Assets/Game/Scenes/Partida.unity";

        [MenuItem("Roba Colores/Fase 2 - Crear escenas Menu y Partida")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Scenes")) AssetDatabase.CreateFolder("Assets/Game", "Scenes");
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Game/Data/GameContent.asset");
            var mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Map/Prefabs/MapaA.prefab");
            if (content == null || mapPrefab == null)
            {
                Debug.LogError("Faltan GameContent.asset o MapaA.prefab. Genera el contenido y el Mapa A primero.");
                return;
            }

            // Menu
            var menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UiKit.Parchment;
            var menuGo = new GameObject("MenuController");
            SetContent(menuGo.AddComponent<MenuController>());
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu, MenuPath);

            // Partida: el mapa (prefab) + el controlador de la partida.
            var game = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Map/Prefabs/MapaA.prefab"));
            var controller = new GameObject("GameController");
            SetContent(controller.AddComponent<GameController>());
            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game, GamePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuPath, true),
                new EditorBuildSettingsScene(GamePath, true),
            };
            AssetDatabase.SaveAssets();
        }

        static void SetContent(Object target)
        {
            // Se carga aqui: NewScene descarga los assets sin uso y dejaria una referencia muerta.
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Game/Data/GameContent.asset");
            var so = new SerializedObject(target);
            so.FindProperty("content").objectReferenceValue = content;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
