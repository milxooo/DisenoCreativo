using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobaColores.Game
{
    static class GameFlowBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RegisterSceneHandler()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "SampleScene")
            {
                if (Object.FindAnyObjectByType<MainMenuController>() == null)
                    new GameObject("Roba Colores - Menu").AddComponent<MainMenuController>();
                return;
            }

            if (scene.name != "MapaA" || Object.FindAnyObjectByType<BoardGameController>() != null)
                return;

            if (LocalGameSetup.Players.Length < 2)
            {
                SceneManager.LoadScene("SampleScene");
                return;
            }

            new GameObject("Roba Colores - Partida").AddComponent<BoardGameController>();
        }
    }
}
