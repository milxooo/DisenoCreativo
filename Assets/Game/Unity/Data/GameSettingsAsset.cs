using System;
using System.Collections.Generic;
using UnityEngine;
using RobaColores.Game.Core;

namespace RobaColores.Game.Presentation
{
    [Serializable]
    public struct MoveFaceData
    {
        public MoveFaceKind kind;
        public int steps;
    }

    // Reglas configurables desde el Inspector. Valores marcados PENDIENTE requieren confirmacion.
    [CreateAssetMenu(menuName = "Roba Colores/Ajustes de partida", fileName = "GameSettings")]
    public class GameSettingsAsset : ScriptableObject
    {
        public int minPlayers = 6;
        public int maxPlayers = 6;
        [Tooltip("PROVISIONAL: si es false, cada jugador debe elegir un personaje distinto. Regla aun sin decidir.")]
        public bool allowRepeatedCharacters;
        public int initialUnitsPerColor = 200;
        public float minigameSeconds = 90f;
        [Tooltip("PENDIENTE de confirmar: segundos de bloqueo al inicio del siguiente minijuego tras sacar la cara 6.")]
        public float timePenaltySeconds = 10f;
        public double scoreCorrect = 1.0;
        public double scoreWrong = 1.0;
        public double scoreMissing = 0.5;
        public List<MoveFaceData> moveDieFaces = new List<MoveFaceData>();

        public GameConfig ToConfig(int paletteSize)
        {
            var cfg = new GameConfig
            {
                MinPlayers = minPlayers,
                MaxPlayers = maxPlayers,
                AllowRepeatedCharacters = allowRepeatedCharacters,
                InitialUnitsPerColor = initialUnitsPerColor,
                MinigameSeconds = minigameSeconds,
                TimePenaltySeconds = timePenaltySeconds,
                PaletteSize = paletteSize,
                Score = new ScoreWeights { Correct = scoreCorrect, Wrong = scoreWrong, Missing = scoreMissing },
            };
            if (moveDieFaces.Count > 0)
            {
                cfg.MoveDieFaces = new MoveFace[moveDieFaces.Count];
                for (int i = 0; i < moveDieFaces.Count; i++) cfg.MoveDieFaces[i] = new MoveFace(moveDieFaces[i].kind, moveDieFaces[i].steps);
            }
            return cfg;
        }
    }
}
