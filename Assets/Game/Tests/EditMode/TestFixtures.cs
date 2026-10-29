using System;
using System.Collections.Generic;
using RobaColores.Game.Core;

namespace RobaColores.Game.Tests
{
    // Aleatorio guionado que acepta cualquier rango (las pruebas controlan el orden de las llamadas).
    public sealed class QueueRandom : IRandom
    {
        readonly Queue<int> values;
        public QueueRandom(params int[] v) { values = new Queue<int>(v); }
        public int Next(int minInclusive, int maxExclusive)
        {
            if (values.Count == 0) throw new InvalidOperationException("QueueRandom agotado");
            int x = values.Dequeue();
            if (x < minInclusive || x >= maxExclusive) throw new InvalidOperationException("Valor " + x + " fuera de [" + minInclusive + "," + maxExclusive + ")");
            return x;
        }
    }

    public static class Fixtures
    {
        // Tablero: 0 INICIO -> 1 (Rojo) -> 2 (Verde, bifurcacion) -> 3 (Azul) / 4 (Naranja) -> 5 META.
        public static BoardGraph SmallBoard()
        {
            var b = new BoardGraph();
            b.Add(new BoardTile { Id = 0, Name = "S", Color = TileColor.Rojo, IsStart = true });
            b.Add(new BoardTile { Id = 1, Name = "T1", Color = TileColor.Rojo });
            b.Add(new BoardTile { Id = 2, Name = "T2", Color = TileColor.Verde });
            b.Add(new BoardTile { Id = 3, Name = "T3", Color = TileColor.Azul });
            b.Add(new BoardTile { Id = 4, Name = "T4", Color = TileColor.Naranja });
            b.Add(new BoardTile { Id = 5, Name = "M", Color = TileColor.Negro, IsFinish = true });
            b.Connect(0, 1); b.Connect(1, 2); b.Connect(2, 3); b.Connect(2, 4); b.Connect(3, 5); b.Connect(4, 5);
            return b;
        }

        public static FigureDefinition Figure(string id, Category cat, Difficulty diff, params int[] cells2x2)
        {
            return new FigureDefinition { Id = id, Name = id, Category = cat, Difficulty = diff, Width = 2, Height = 2, Cells = cells2x2 };
        }

        // Una figura 2x2 de prueba por categoria (NO es el catalogo real del juego).
        public static FigureCatalog Catalog()
        {
            var c = new FigureCatalog();
            foreach (Category cat in Enum.GetValues(typeof(Category)))
                c.Add(Figure("test_" + cat, cat, Difficulty.Facil, 0, 1, 1, -1));
            return c;
        }

        public static GameSession Session(IRandom rng, int players = 2)
        {
            var s = new GameSession(new GameConfig(), SmallBoard(), Catalog(), rng);
            for (int i = 0; i < players; i++) s.AddPlayer("J" + i, i);
            return s;
        }

        public static void StartAndRollOrder(GameSession session)
        {
            session.StartGame();
            while (session.Phase == GamePhase.AwaitingOrderRoll) session.RollOrderDie();
        }
    }
}
