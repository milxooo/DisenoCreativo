using System;
using System.Collections.Generic;

namespace RobaColores.Game.Core
{
    // Unica fuente de verdad de los recursos de un jugador. La cuadricula y la UI solo consultan o piden operaciones.
    public sealed class Inventory
    {
        readonly int[] units;

        public Inventory(int colorCount, int initialUnitsPerColor)
        {
            if (colorCount <= 0 || initialUnitsPerColor < 0) throw new ArgumentException();
            units = new int[colorCount];
            for (int i = 0; i < colorCount; i++) units[i] = initialUnitsPerColor;
        }

        public int ColorCount { get { return units.Length; } }
        public int Get(int color) { return units[color]; }

        public int Total
        {
            get { int t = 0; foreach (var u in units) t += u; return t; }
        }

        public bool Has(int color, int amount) { return color >= 0 && color < units.Length && amount >= 0 && units[color] >= amount; }

        // Resta unidades si las hay. Nunca deja cantidades negativas.
        public bool TryTake(int color, int amount)
        {
            if (!Has(color, amount)) return false;
            units[color] -= amount;
            return true;
        }

        public void Add(int color, int amount)
        {
            if (color < 0 || color >= units.Length || amount < 0) throw new ArgumentException();
            units[color] += amount;
        }

        public int[] Snapshot() { return (int[])units.Clone(); }

        public bool CanCover(int[] required)
        {
            for (int i = 0; i < required.Length; i++)
                if (required[i] > 0 && (i >= units.Length || units[i] < required[i])) return false;
            return true;
        }
    }

    public sealed class PlayerState
    {
        public int Id;
        public string Name;
        public int CharacterId;
        public int TileId;
        public Inventory Inventory;
        public bool Disqualified;
        public bool SkipNextTurn;          // turno perdido pendiente (se consume al empezar su siguiente turno)
        public float PendingTimePenalty;   // segundos de bloqueo al inicio del siguiente minijuego en el que participe
        public bool IsActive { get { return !Disqualified; } }
    }

    public enum PaintResult { Ok, NoChange, Finished, Locked, OutOfBounds, NoUnits, InvalidColor, NotParticipant }

    public enum FinishReason { None, Timeout, PlayerPressed }

    // Un minijuego en curso. Hay UN unico temporizador y UN unico evento de fin para todos los participantes.
    public sealed class MinigameRound
    {
        readonly Dictionary<int, PlayerState> players = new Dictionary<int, PlayerState>();
        readonly Dictionary<int, PixelGrid> grids = new Dictionary<int, PixelGrid>();
        readonly Dictionary<int, float> lockSeconds = new Dictionary<int, float>();
        readonly int paletteSize;

        public FigureDefinition Figure { get; private set; }
        public Category Category { get; private set; }
        public float TimeLimit { get; private set; }
        public float Elapsed { get; private set; }
        public bool Finished { get; private set; }
        public FinishReason Reason { get; private set; }
        public int FinishedByPlayerId { get; private set; }

        public MinigameRound(FigureDefinition figure, Category category, IEnumerable<PlayerState> participants, float timeLimit, int paletteSize)
        {
            Figure = figure; Category = category; TimeLimit = timeLimit; this.paletteSize = paletteSize;
            FinishedByPlayerId = -1;
            foreach (var p in participants)
            {
                players[p.Id] = p;
                grids[p.Id] = new PixelGrid(figure.Width, figure.Height);   // cuadricula propia de cada jugador
                lockSeconds[p.Id] = p.PendingTimePenalty;                    // penalizacion de la cara 6
                p.PendingTimePenalty = 0f;
            }
        }

        public IEnumerable<int> ParticipantIds { get { return players.Keys; } }
        public bool IsParticipant(int playerId) { return players.ContainsKey(playerId); }
        public float TimeRemaining { get { return Math.Max(0f, TimeLimit - Elapsed); } }
        public float LockRemaining(int playerId) { return Math.Max(0f, lockSeconds[playerId] - Elapsed); }
        public PixelGrid GridOf(int playerId) { return grids[playerId]; }

        public void Tick(float deltaSeconds)
        {
            if (Finished || deltaSeconds <= 0f) return;
            Elapsed += deltaSeconds;
            if (Elapsed >= TimeLimit) Finish(FinishReason.Timeout, -1);
        }

        // Cualquier participante puede terminar: el minijuego acaba para todos de inmediato.
        public bool PressFinish(int playerId)
        {
            if (Finished || !players.ContainsKey(playerId)) return false;
            Finish(FinishReason.PlayerPressed, playerId);
            return true;
        }

        void Finish(FinishReason reason, int byPlayer)
        {
            Finished = true; Reason = reason; FinishedByPlayerId = byPlayer;
            if (Elapsed > TimeLimit) Elapsed = TimeLimit;
        }

        // Pintar o cambiar un pixel. Regla: pintar consume 1 unidad del color nuevo; si la celda ya tenia otro color,
        // se devuelve esa unidad (cambiar no cuesta dos). Al terminar el minijuego lo gastado queda gastado.
        public PaintResult TryPaint(int playerId, int x, int y, int color)
        {
            if (!players.ContainsKey(playerId)) return PaintResult.NotParticipant;
            if (Finished) return PaintResult.Finished;
            if (LockRemaining(playerId) > 0f) return PaintResult.Locked;
            var grid = grids[playerId];
            if (!grid.InBounds(x, y)) return PaintResult.OutOfBounds;
            if (color < 0 || color >= paletteSize) return PaintResult.InvalidColor;

            int old = grid.Get(x, y);
            if (old == color) return PaintResult.NoChange;
            var inv = players[playerId].Inventory;
            if (!inv.TryTake(color, 1)) return PaintResult.NoUnits;
            if (old >= 0) inv.Add(old, 1);
            grid.SetInternal(x, y, color);
            return PaintResult.Ok;
        }

        public PaintResult TryErase(int playerId, int x, int y)
        {
            if (!players.ContainsKey(playerId)) return PaintResult.NotParticipant;
            if (Finished) return PaintResult.Finished;
            if (LockRemaining(playerId) > 0f) return PaintResult.Locked;
            var grid = grids[playerId];
            if (!grid.InBounds(x, y)) return PaintResult.OutOfBounds;
            int old = grid.Get(x, y);
            if (old < 0) return PaintResult.NoChange;
            players[playerId].Inventory.Add(old, 1);
            grid.SetInternal(x, y, PixelGrid.Empty);
            return PaintResult.Ok;
        }

        public Dictionary<int, EvaluationResult> Evaluate(ScoreWeights weights)
        {
            var res = new Dictionary<int, EvaluationResult>();
            foreach (var kv in grids) res[kv.Key] = FigureEvaluator.Evaluate(Figure, kv.Value, weights);
            return res;
        }

        internal void ClearRowInternal(int playerId, int row) { grids[playerId].ClearRow(row); }
    }
}
