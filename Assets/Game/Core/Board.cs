using System;
using System.Collections.Generic;

namespace RobaColores.Game.Core
{
    public sealed class BoardTile
    {
        public int Id;
        public string Name;
        public TileColor Color;
        public Difficulty Difficulty = Difficulty.Facil;
        public bool IsStart;
        public bool IsFinish;
        public bool TriggersMinigame = true;           // configurable por casilla
        public List<int> Next = new List<int>();       // conexiones hacia adelante (INICIO -> META)
    }

    // Representacion logica del tablero: casillas y conexiones dirigidas. No depende de Unity.
    public sealed class BoardGraph
    {
        readonly Dictionary<int, BoardTile> tiles = new Dictionary<int, BoardTile>();

        public int StartId { get; private set; } = -1;
        public int FinishId { get; private set; } = -1;
        public int Count { get { return tiles.Count; } }
        public IEnumerable<BoardTile> Tiles { get { return tiles.Values; } }

        public BoardTile Add(BoardTile tile)
        {
            if (tiles.ContainsKey(tile.Id)) throw new ArgumentException("Casilla duplicada: " + tile.Id);
            tiles[tile.Id] = tile;
            if (tile.IsStart) StartId = tile.Id;
            if (tile.IsFinish) FinishId = tile.Id;
            if (tile.IsStart || tile.IsFinish) tile.TriggersMinigame = false;
            return tile;
        }

        public BoardTile Get(int id)
        {
            BoardTile t;
            if (!tiles.TryGetValue(id, out t)) throw new KeyNotFoundException("Casilla inexistente: " + id);
            return t;
        }

        public void Connect(int from, int to)
        {
            var a = Get(from); Get(to);
            if (!a.Next.Contains(to)) a.Next.Add(to);
        }

        public IList<int> NextOf(int id) { return Get(id).Next; }

        // Comprueba que el tablero es jugable: hay inicio y meta, todo es alcanzable desde INICIO y toda casilla
        // (salvo la meta) tiene salida. Devuelve la lista de problemas (vacia si esta bien).
        public List<string> Validate()
        {
            var problems = new List<string>();
            if (StartId < 0) problems.Add("Falta la casilla INICIO.");
            if (FinishId < 0) problems.Add("Falta la casilla META.");
            if (problems.Count > 0) return problems;

            var reach = new HashSet<int>();
            var stack = new Stack<int>();
            stack.Push(StartId);
            while (stack.Count > 0)
            {
                int id = stack.Pop();
                if (!reach.Add(id)) continue;
                foreach (var n in Get(id).Next) stack.Push(n);
            }
            foreach (var t in tiles.Values)
            {
                if (!reach.Contains(t.Id)) problems.Add("Casilla inalcanzable desde INICIO: " + t.Name);
                if (!t.IsFinish && t.Next.Count == 0) problems.Add("Casilla sin salida: " + t.Name);
                if (t.IsFinish && t.Next.Count > 0) problems.Add("La META no debe tener salidas.");
            }
            return problems;
        }

        // Longitudes (en pasos) de todas las rutas INICIO -> META.
        public List<int> RouteLengths()
        {
            var res = new List<int>();
            Walk(StartId, 0, res);
            return res;
        }

        void Walk(int id, int steps, List<int> res)
        {
            var t = Get(id);
            if (t.IsFinish) { res.Add(steps); return; }
            foreach (var n in t.Next) Walk(n, steps + 1, res);
        }
    }
}
