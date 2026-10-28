using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using RobaColores.Game.Core;
using RobaColores.Game.Presentation;
using RobaColores.Map;

namespace RobaColores.Game.EditorTools
{
    // Simula partidas completas con bots sobre el tablero REAL del Mapa A (abierto en la escena) y el catalogo real.
    // Comprueba que las partidas terminan, que los recursos nunca son negativos ni aumentan de la nada.
    public static class BotSimulation
    {
        [MenuItem("Roba Colores/Simular partidas con bots")]
        public static void RunMenu() { Debug.Log(Run(40)); }

        public static string Run(int games)
        {
            var board = UnityEngine.Object.FindFirstObjectByType<MapBoard>();
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Game/Data/GameContent.asset");
            if (board == null) return "Abre la escena MapaA (o Partida) antes de simular.";
            if (content == null) return "Falta Assets/Game/Data/GameContent.asset (menu Roba Colores > Fase 2 - Generar contenido del juego).";

            int finishWins = 0, elimWins = 0, noWinner = 0, stuck = 0, violations = 0, totalTurns = 0, maxTurns = 0, minigames = 0, robberies = 0;
            var errors = new StringBuilder();
            for (int g = 0; g < games; g++)
            {
                var rng = new SystemRandomSource(1000 + g);
                var cfg = content.BuildConfig();
                var session = new GameSession(cfg, board.BuildGraph(), content.BuildCatalog(), rng);
                int players = 2 + g % 3;
                for (int i = 0; i < players; i++) session.AddPlayer("Bot" + i, i);
                double accuracy = 0.55 + 0.45 * ((g * 37) % 100) / 100.0;
                int turns = 0;
                try
                {
                    session.StartGame();
                    while (session.Phase != GamePhase.GameOver && turns < 3000)
                    {
                        turns++;
                        switch (session.Phase)
                        {
                            case GamePhase.AwaitingMoveRoll: session.RollMoveDie(); break;
                            case GamePhase.AwaitingPathChoice:
                                session.ChoosePath(session.PathOptions[rng.Next(0, session.PathOptions.Count)]); break;
                            case GamePhase.MinigameRunning:
                                minigames++;
                                int before = TotalUnits(session);
                                PlayRound(session, rng, accuracy);
                                if (session.Phase == GamePhase.AwaitingRowSelection) { PickRows(session, rng); robberies++; }
                                if (TotalUnits(session) > before) { violations++; errors.AppendLine("Partida " + g + ": las unidades aumentaron."); }
                                break;
                            default: throw new InvalidOperationException("Fase inesperada " + session.Phase);
                        }
                        foreach (var p in session.Players)
                            for (int c = 0; c < cfg.PaletteSize; c++)
                                if (p.Inventory.Get(c) < 0) { violations++; errors.AppendLine("Inventario negativo en partida " + g); }
                    }
                }
                catch (Exception e) { violations++; errors.AppendLine("Excepcion en partida " + g + ": " + e.Message); }

                if (session.Phase != GamePhase.GameOver) stuck++;
                else if (session.Outcome.Kind == OutcomeKind.WinByFinish) finishWins++;
                else if (session.Outcome.Kind == OutcomeKind.WinByElimination) elimWins++;
                else noWinner++;
                totalTurns += turns; maxTurns = Math.Max(maxTurns, turns);
            }
            return "Simulacion de " + games + " partidas: llegada a meta " + finishWins + ", eliminacion " + elimWins + ", sin ganador " + noWinner +
                   ", atascadas " + stuck + ", violaciones " + violations + " | minijuegos " + minigames + ", robos " + robberies +
                   " | acciones por partida: media " + (totalTurns / Math.Max(1, games)) + ", maxima " + maxTurns + "\n" + errors;
        }

        static int TotalUnits(GameSession s)
        {
            int t = 0; foreach (var p in s.Players) t += p.Inventory.Total; return t;
        }

        static void PlayRound(GameSession s, IRandom rng, double accuracy)
        {
            var round = s.Round; var fig = round.Figure; int palette = s.Config.PaletteSize;
            foreach (var pid in new List<int>(round.ParticipantIds))
            {
                for (int y = 0; y < fig.Height; y++)
                    for (int x = 0; x < fig.Width; x++)
                    {
                        int want = fig.CellAt(x, y);
                        double roll = rng.Next(0, 1000) / 1000.0;
                        if (roll < accuracy) { if (want >= 0) round.TryPaint(pid, x, y, want); }
                        else round.TryPaint(pid, x, y, rng.Next(0, palette));
                        if (rng.Next(0, 20) == 0) round.TryErase(pid, x, y);
                    }
            }
            var ids = new List<int>(round.ParticipantIds);
            s.PressFinish(ids[rng.Next(0, ids.Count)]);
        }

        static void PickRows(GameSession s, IRandom rng)
        {
            var sel = new Dictionary<int, IDictionary<int, int>>();
            var winners = new List<int>(s.RoundWinners);
            foreach (var w in winners)
            {
                var byOpp = new Dictionary<int, int>();
                foreach (var o in RowRobbery.Opponents(winners, s.Round)) byOpp[o] = rng.Next(0, s.Round.Figure.Height);
                sel[w] = byOpp;
            }
            s.ResolveRewards(sel);
        }
    }
}
