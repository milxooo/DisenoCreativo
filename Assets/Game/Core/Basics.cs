using System;
using System.Collections.Generic;

namespace RobaColores.Game.Core
{
    // Mismo orden que PathTileColor del mapa (Rojo, Naranja, Amarillo, Verde, Azul, Morado, Negro).
    public enum TileColor { Rojo, Naranja, Amarillo, Verde, Azul, Morado, Negro }

    public enum Category { PartesDelCuerpo, FrutasYVerduras, AccesoriosDeRopa, Animales, Profesiones, Deportes, Astronomia }

    public enum Difficulty { Facil, Intermedio, Dificil }

    public enum MoveFaceKind { Advance, LoseTurn, TimePenalty }

    // Una cara del dado de movimiento: avanzar N casillas o un castigo.
    public struct MoveFace
    {
        public MoveFaceKind Kind;
        public int Steps;

        public MoveFace(MoveFaceKind kind, int steps = 0) { Kind = kind; Steps = steps; }
        public static MoveFace Advance(int steps) { return new MoveFace(MoveFaceKind.Advance, steps); }
    }

    // Pesos de la formula de puntuacion (modificables).
    public sealed class ScoreWeights
    {
        public double Correct = 1.0;     // pixel con el color correcto
        public double Wrong = 1.0;       // pixel pintado con color equivocado o donde la referencia esta vacia
        public double Missing = 0.5;     // pixel de la referencia que sigue sin pintar
    }

    // Toda la configuracion jugable en un solo objeto. Los valores marcados PENDIENTE requieren confirmacion.
    public sealed class GameConfig
    {
        public int MinPlayers = 2;
        public int MaxPlayers = 4;
        public int InitialUnitsPerColor = 200;
        public float MinigameSeconds = 90f;
        // PENDIENTE de confirmar: segundos que la cuadricula del jugador queda bloqueada al inicio del siguiente minijuego.
        public float TimePenaltySeconds = 10f;
        public int PaletteSize = 8;
        public ScoreWeights Score = new ScoreWeights();
        // Si nadie supera esta puntuacion no hay ganador del minijuego (nadie roba).
        public double MinWinningScore = 0.0001;

        public MoveFace[] MoveDieFaces = new[]
        {
            MoveFace.Advance(1), MoveFace.Advance(2), MoveFace.Advance(3), MoveFace.Advance(4),
            new MoveFace(MoveFaceKind.LoseTurn), new MoveFace(MoveFaceKind.TimePenalty),
        };

        // Categoria de cada color de casilla (datos, no depende del material).
        public Dictionary<TileColor, Category> CategoryByColor = new Dictionary<TileColor, Category>
        {
            { TileColor.Naranja, Category.FrutasYVerduras },
            { TileColor.Rojo, Category.PartesDelCuerpo },
            { TileColor.Amarillo, Category.AccesoriosDeRopa },
            { TileColor.Verde, Category.Animales },
            { TileColor.Azul, Category.Profesiones },
            { TileColor.Morado, Category.Deportes },
            { TileColor.Negro, Category.Astronomia },
        };
    }

    public interface IRandom
    {
        int Next(int minInclusive, int maxExclusive);
    }

    public sealed class SystemRandomSource : IRandom
    {
        readonly Random random;
        public SystemRandomSource(int seed) { random = new Random(seed); }
        public SystemRandomSource() { random = new Random(); }
        public int Next(int minInclusive, int maxExclusive) { return random.Next(minInclusive, maxExclusive); }
    }

    // Aleatorio guionado para pruebas: devuelve los valores en orden (ciclico).
    public sealed class ScriptedRandom : IRandom
    {
        readonly int[] values;
        int index;
        public ScriptedRandom(params int[] values) { this.values = values; }
        public int Next(int minInclusive, int maxExclusive)
        {
            int v = values[index++ % values.Length];
            if (v < minInclusive || v >= maxExclusive) throw new InvalidOperationException("Valor guionado fuera de rango: " + v);
            return v;
        }
    }

    // Dado para decidir el orden inicial: seis caras numericas, 1 a 6.
    public sealed class InitialDie
    {
        public int Roll(IRandom rng) { return rng.Next(1, 7); }
    }

    // Dado de movimiento: caras configurables.
    public sealed class MoveDie
    {
        readonly MoveFace[] faces;
        public MoveDie(MoveFace[] faces)
        {
            if (faces == null || faces.Length == 0) throw new ArgumentException("El dado necesita caras.");
            this.faces = faces;
        }
        public int FaceCount { get { return faces.Length; } }
        public MoveFace FaceAt(int i) { return faces[i]; }
        public MoveFace Roll(IRandom rng) { return faces[rng.Next(0, faces.Length)]; }
    }

    public sealed class TurnOrderResult
    {
        public List<int> Order = new List<int>();                    // ids de jugador, el primero empieza
        public List<Dictionary<int, int>> Rounds = new List<Dictionary<int, int>>();  // tiradas por ronda (para mostrar)
    }

    public static class TurnOrderResolver
    {
        // Cada jugador tira el dado inicial. Mayor resultado primero. Los empates se vuelven a tirar entre los
        // empatados (tambien los empates en posiciones inferiores, para obtener un orden estricto).
        public static TurnOrderResult Resolve(IList<int> playerIds, InitialDie die, IRandom rng)
        {
            var result = new TurnOrderResult();
            var pending = new List<int>(playerIds);
            Resolve(pending, die, rng, result);
            return result;
        }

        static void Resolve(List<int> group, InitialDie die, IRandom rng, TurnOrderResult result)
        {
            if (group.Count == 1) { result.Order.Add(group[0]); return; }
            var rolls = new Dictionary<int, int>();
            foreach (var id in group) rolls[id] = die.Roll(rng);
            result.Rounds.Add(rolls);

            var values = new List<int>(rolls.Values);
            values.Sort((a, b) => b.CompareTo(a));
            var seen = new HashSet<int>();
            foreach (var v in values)
            {
                if (!seen.Add(v)) continue;
                var tied = new List<int>();
                foreach (var id in group) if (rolls[id] == v) tied.Add(id);
                if (tied.Count == 1) result.Order.Add(tied[0]);
                else Resolve(tied, die, rng, result);
            }
        }
    }
}
