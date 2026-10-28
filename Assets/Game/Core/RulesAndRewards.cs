using System;
using System.Collections.Generic;

namespace RobaColores.Game.Core
{
    public sealed class RobberyTransfer
    {
        public int WinnerId;
        public int OpponentId;
        public int Row;
        public int[] Units;      // unidades por color que gana el ganador
    }

    // Robo de filas (regla elegida: "dibujar consume"):
    // - Las unidades que un oponente pinto ya se gastaron al dibujar.
    // - El ganador elige una fila de la cuadricula final de cada oponente y recupera los colores de esa fila.
    // - Con k ganadores (empate), cada uno recibe floor(unidades / k) por color: nunca se crean recursos.
    // - Las celdas vacias no aportan nada. Las filas robadas se vacian en la cuadricula del oponente.
    public static class RowRobbery
    {
        public static List<string> Validate(IList<int> winnerIds, MinigameRound round, IDictionary<int, IDictionary<int, int>> selections)
        {
            var errors = new List<string>();
            var opponents = Opponents(winnerIds, round);
            foreach (var w in winnerIds)
            {
                IDictionary<int, int> byOpponent;
                if (selections == null || !selections.TryGetValue(w, out byOpponent)) { errors.Add("Falta la seleccion del ganador " + w); continue; }
                foreach (var o in opponents)
                {
                    int row;
                    if (!byOpponent.TryGetValue(o, out row)) { errors.Add("El ganador " + w + " no eligio fila del oponente " + o); continue; }
                    if (row < 0 || row >= round.Figure.Height) errors.Add("Fila invalida: " + row);
                }
                foreach (var key in byOpponent.Keys)
                    if (!opponents.Contains(key)) errors.Add("El ganador " + w + " eligio a alguien que no es oponente: " + key);
            }
            return errors;
        }

        public static List<int> Opponents(IList<int> winnerIds, MinigameRound round)
        {
            var res = new List<int>();
            foreach (var id in round.ParticipantIds) if (!winnerIds.Contains(id)) res.Add(id);
            return res;
        }

        public static List<RobberyTransfer> Resolve(IList<int> winnerIds, IDictionary<int, PlayerState> players, MinigameRound round,
            IDictionary<int, IDictionary<int, int>> selections, int paletteSize)
        {
            var errors = Validate(winnerIds, round, selections);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("; ", errors.ToArray()));

            int k = winnerIds.Count;
            var transfers = new List<RobberyTransfer>();
            var opponents = Opponents(winnerIds, round);

            // 1) Calcular todo sobre las cuadriculas tal como terminaron (sin que un robo afecte al siguiente).
            foreach (var w in winnerIds)
                foreach (var o in opponents)
                {
                    int row = selections[w][o];
                    var counts = round.GridOf(o).RowCounts(row, paletteSize);
                    var share = new int[paletteSize];
                    for (int c = 0; c < paletteSize; c++) share[c] = counts[c] / k;
                    transfers.Add(new RobberyTransfer { WinnerId = w, OpponentId = o, Row = row, Units = share });
                }

            // 2) Aplicar: el ganador recibe las unidades y las filas robadas se vacian.
            foreach (var t in transfers)
                for (int c = 0; c < paletteSize; c++)
                    if (t.Units[c] > 0) players[t.WinnerId].Inventory.Add(c, t.Units[c]);
            var cleared = new HashSet<string>();
            foreach (var t in transfers)
                if (cleared.Add(t.OpponentId + ":" + t.Row)) round.ClearRowInternal(t.OpponentId, t.Row);
            return transfers;
        }
    }

    public static class Rules
    {
        // Un jugador puede seguir si con sus unidades puede construir al menos una figura valida del catalogo.
        public static bool CanBuildAnyFigure(Inventory inventory, FigureCatalog catalog, int paletteSize)
        {
            foreach (var f in catalog.All)
                if (inventory.CanCover(f.RequiredUnits(paletteSize))) return true;
            return false;
        }

        // Marca como descalificados a los jugadores activos que ya no pueden construir ninguna figura.
        // Un descalificado no vuelve a jugar aunque reciba recursos despues.
        public static List<PlayerState> UpdateDisqualifications(IEnumerable<PlayerState> players, FigureCatalog catalog, int paletteSize)
        {
            var newly = new List<PlayerState>();
            foreach (var p in players)
            {
                if (p.Disqualified) continue;
                if (!CanBuildAnyFigure(p.Inventory, catalog, paletteSize)) { p.Disqualified = true; newly.Add(p); }
            }
            return newly;
        }
    }

    public enum OutcomeKind { None, WinByFinish, WinByElimination, NoWinner }

    public sealed class GameOutcome
    {
        public OutcomeKind Kind;
        public int WinnerId = -1;
        public static GameOutcome Continue { get { return new GameOutcome { Kind = OutcomeKind.None }; } }
    }

    public static class VictoryChecker
    {
        // Victoria por eliminacion. Si todos quedan descalificados a la vez no hay ganador (partida sin ganador).
        public static GameOutcome CheckElimination(IEnumerable<PlayerState> players)
        {
            PlayerState last = null; int active = 0;
            foreach (var p in players) if (p.IsActive) { active++; last = p; }
            if (active == 0) return new GameOutcome { Kind = OutcomeKind.NoWinner };
            if (active == 1) return new GameOutcome { Kind = OutcomeKind.WinByElimination, WinnerId = last.Id };
            return GameOutcome.Continue;
        }
    }
}
