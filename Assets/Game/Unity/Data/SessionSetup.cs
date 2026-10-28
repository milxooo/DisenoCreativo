using System.Collections.Generic;

namespace RobaColores.Game.Presentation
{
    // Seleccion hecha en el menu que la escena de partida lee. Solo para un dispositivo; con red se sustituye.
    public static class SessionSetup
    {
        public static List<int> CharacterIndices = new List<int>();
        public static List<string> Names = new List<string>();

        public static void Set(List<int> characters, List<string> names)
        {
            CharacterIndices = new List<int>(characters);
            Names = new List<string>(names);
        }

        public static bool HasSelection { get { return CharacterIndices.Count >= 2; } }
    }
}
