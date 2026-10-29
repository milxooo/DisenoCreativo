using System;
using System.Collections.Generic;
using RobaColores.Game.Core;

namespace RobaColores.Game.Presentation
{
    [Serializable]
    public struct PlayerSetup
    {
        public int number;          // 1..N
        public int characterIndex;  // posicion en GameContent.characters
        public string name;
    }

    // Seleccion confirmada en el menu que la escena de partida lee. Solo para un dispositivo; con red se sustituye.
    public static class SessionSetup
    {
        static List<PlayerSetup> players = new List<PlayerSetup>();

        public static IList<PlayerSetup> Players { get { return players; } }
        public static bool HasSelection { get { return players.Count > 0; } }

        // Guarda la seleccion. Falla si no es valida: asi la partida nunca recibe datos incompletos.
        public static void Commit(CharacterSelection selection)
        {
            var characters = selection.ConfirmedCharacters();   // lanza si falta alguien o hay repetidos no permitidos
            var list = new List<PlayerSetup>();
            for (int i = 0; i < characters.Length; i++)
                list.Add(new PlayerSetup { number = i + 1, characterIndex = characters[i], name = "Jugador " + (i + 1) });
            players = list;
        }

        public static void Clear() { players = new List<PlayerSetup>(); }
    }
}
