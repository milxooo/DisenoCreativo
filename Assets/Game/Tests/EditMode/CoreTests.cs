using System;
using System.Collections.Generic;
using NUnit.Framework;
using RobaColores.Game.Core;

namespace RobaColores.Game.Tests
{
    public class DiceTests
    {
        [Test]
        public void InitialDie_OnlyProducesOneToSix()
        {
            var die = new InitialDie();
            var rng = new SystemRandomSource(1);
            var seen = new HashSet<int>();
            for (int i = 0; i < 500; i++) { int v = die.Roll(rng); Assert.IsTrue(v >= 1 && v <= 6); seen.Add(v); }
            Assert.AreEqual(6, seen.Count);
        }

        [Test]
        public void MoveDie_HasFourAdvanceFacesAndTwoPunishments()
        {
            var cfg = new GameConfig();
            var die = new MoveDie(cfg.MoveDieFaces);
            Assert.AreEqual(6, die.FaceCount);
            int adv = 0, lose = 0, pen = 0;
            for (int i = 0; i < die.FaceCount; i++)
            {
                var f = die.FaceAt(i);
                if (f.Kind == MoveFaceKind.Advance) { adv++; Assert.AreEqual(i + 1, f.Steps); }
                if (f.Kind == MoveFaceKind.LoseTurn) lose++;
                if (f.Kind == MoveFaceKind.TimePenalty) pen++;
            }
            Assert.AreEqual(4, adv); Assert.AreEqual(1, lose); Assert.AreEqual(1, pen);
        }

        [Test]
        public void MoveDie_IsConfigurable()
        {
            var die = new MoveDie(new[] { MoveFace.Advance(6), new MoveFace(MoveFaceKind.LoseTurn) });
            Assert.AreEqual(6, die.Roll(new QueueRandom(0)).Steps);
            Assert.AreEqual(MoveFaceKind.LoseTurn, die.Roll(new QueueRandom(1)).Kind);
        }
    }

    public class TurnOrderTests
    {
        [Test]
        public void PlayersRollForTheStartingOrderOneAtATime()
        {
            var s = Fixtures.Session(new QueueRandom(2, 6));
            s.StartGame();

            Assert.AreEqual(GamePhase.AwaitingOrderRoll, s.Phase);
            Assert.AreEqual(0, s.CurrentOrderPlayerId);
            Assert.AreEqual(2, s.RollOrderDie());
            Assert.AreEqual(1, s.CurrentOrderPlayerId);
            Assert.AreEqual(GamePhase.AwaitingOrderRoll, s.Phase);
            Assert.AreEqual(6, s.RollOrderDie());
            Assert.AreEqual(GamePhase.AwaitingMoveRoll, s.Phase);
            Assert.AreEqual(1, s.CurrentPlayer.Id);
            CollectionAssert.AreEqual(new[] { 1, 0 }, new List<int>(s.TurnOrder));
        }

        [Test]
        public void TiedStartingRollsAreRepeatedInTurnOrderOnlyByTiedPlayers()
        {
            var s = Fixtures.Session(new QueueRandom(5, 5, 1, 6));
            s.StartGame();
            while (s.Phase == GamePhase.AwaitingOrderRoll) s.RollOrderDie();

            Assert.AreEqual(2, s.OrderResult.Rounds.Count);
            Assert.AreEqual(2, s.OrderResult.Rounds[1].Count);
            CollectionAssert.AreEqual(new[] { 1, 0 }, new List<int>(s.TurnOrder));
        }

        [Test]
        public void HighestRollGoesFirst()
        {
            var r = TurnOrderResolver.Resolve(new[] { 0, 1, 2 }, new InitialDie(), new QueueRandom(2, 6, 4));
            CollectionAssert.AreEqual(new[] { 1, 2, 0 }, r.Order);
        }

        [Test]
        public void TieForHighestIsRerolledOnlyAmongTied()
        {
            // J0=5, J1=5, J2=2 -> empate J0/J1 -> se repite solo entre ellos: J0=1, J1=3.
            var r = TurnOrderResolver.Resolve(new[] { 0, 1, 2 }, new InitialDie(), new QueueRandom(5, 5, 2, 1, 3));
            CollectionAssert.AreEqual(new[] { 1, 0, 2 }, r.Order);
            Assert.AreEqual(2, r.Rounds.Count);
            Assert.AreEqual(2, r.Rounds[1].Count);
        }

        [Test]
        public void LowerTiesAreAlsoResolvedForStrictOrder()
        {
            var r = TurnOrderResolver.Resolve(new[] { 0, 1, 2 }, new InitialDie(), new QueueRandom(6, 2, 2, 5, 1));
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, r.Order);
        }
    }

    public class BoardTests
    {
        [Test]
        public void ValidBoardHasNoProblems()
        {
            Assert.AreEqual(0, Fixtures.SmallBoard().Validate().Count);
        }

        [Test]
        public void DeadEndAndUnreachableTilesAreReported()
        {
            var b = Fixtures.SmallBoard();
            b.Add(new BoardTile { Id = 9, Name = "isla", Color = TileColor.Azul });
            var problems = b.Validate();
            Assert.IsTrue(problems.Count >= 2);
        }

        [Test]
        public void AllRoutesHaveSameLength()
        {
            var lengths = Fixtures.SmallBoard().RouteLengths();
            Assert.AreEqual(2, lengths.Count);
            Assert.AreEqual(lengths[0], lengths[1]);
        }
    }

    public class MovementTests
    {
        // Valores: orden (J0=2, J1=6 -> empieza J1), tirada de movimiento por indice de cara.
        [Test]
        public void MoveStopsAtForkAndWaitsForChoice()
        {
            var s = Fixtures.Session(new QueueRandom(2, 6, 3, 0, 0)); // J1 saca Avanzar 4... (indice 3)
            Fixtures.StartAndRollOrder(s);
            Assert.AreEqual(1, s.CurrentPlayer.Id);
            s.RollMoveDie();
            Assert.AreEqual(GamePhase.AwaitingPathChoice, s.Phase);
            Assert.AreEqual(2, s.CurrentPlayer.TileId);
            CollectionAssert.AreEquivalent(new[] { 3, 4 }, new List<int>(s.PathOptions));
        }

        [Test]
        public void InvalidPathIsRejected()
        {
            var s = Fixtures.Session(new QueueRandom(2, 6, 3));
            Fixtures.StartAndRollOrder(s); s.RollMoveDie();
            Assert.Throws<InvalidOperationException>(() => s.ChoosePath(1));
            Assert.Throws<InvalidOperationException>(() => s.ChoosePath(5));
        }

        [Test]
        public void ExactStepsAreWalkedAndFinishWins()
        {
            // Avanzar 4 desde INICIO: 0->1->2(bifurcacion). Elegir 3 -> consume paso 3 y luego 5 (META).
            var s = Fixtures.Session(new QueueRandom(2, 6, 3));
            Fixtures.StartAndRollOrder(s); s.RollMoveDie();
            s.ChoosePath(3);
            Assert.AreEqual(GamePhase.GameOver, s.Phase);
            Assert.AreEqual(OutcomeKind.WinByFinish, s.Outcome.Kind);
            Assert.AreEqual(1, s.Outcome.WinnerId);
            Assert.AreEqual(5, s.GetPlayer(1).TileId);
        }

        [Test]
        public void LandingOnColoredTileStartsMinigameOfItsCategory()
        {
            var s = Fixtures.Session(new QueueRandom(2, 6, 0, 0)); // Avanzar 1 -> casilla 1 (Rojo)
            Fixtures.StartAndRollOrder(s); s.RollMoveDie();
            Assert.AreEqual(GamePhase.MinigameRunning, s.Phase);
            Assert.AreEqual(Category.PartesDelCuerpo, s.Round.Category);
            Assert.AreEqual(Category.PartesDelCuerpo, s.Round.Figure.Category);
        }

        [Test]
        public void LoseTurnSkipsTheNextTurnOfThatPlayerOnce()
        {
            // J1 empieza, saca LoseTurn (indice 4). J0 juega (Avanzar 1 -> minijuego). Luego J1 pierde su turno.
            var s = Fixtures.Session(new QueueRandom(2, 6, 4, 0, 0));
            Fixtures.StartAndRollOrder(s);
            s.RollMoveDie();
            Assert.AreEqual(0, s.CurrentPlayer.Id);
            Assert.IsTrue(s.GetPlayer(1).SkipNextTurn);
            s.RollMoveDie();                 // J0 -> minijuego
            s.PressFinish(0);                // sin dibujar: nadie gana
            Assert.AreEqual(GamePhase.AwaitingMoveRoll, s.Phase);
            Assert.AreEqual(0, s.CurrentPlayer.Id, "J1 pierde el turno y vuelve a jugar J0");
            Assert.IsFalse(s.GetPlayer(1).SkipNextTurn, "el turno perdido se consume una sola vez");
        }

        [Test]
        public void TimePenaltyIsStoredForNextMinigame()
        {
            var s = Fixtures.Session(new QueueRandom(2, 6, 5, 0, 0));
            Fixtures.StartAndRollOrder(s);
            s.RollMoveDie();                 // J1: penalizacion
            Assert.AreEqual(s.Config.TimePenaltySeconds, s.GetPlayer(1).PendingTimePenalty);
            s.RollMoveDie();                 // J0 cae en casilla 1 -> minijuego
            Assert.AreEqual(s.Config.TimePenaltySeconds, s.Round.LockRemaining(1), 0.0001f);
            Assert.AreEqual(0f, s.Round.LockRemaining(0));
            Assert.AreEqual(0f, s.GetPlayer(1).PendingTimePenalty, "se consume al empezar el minijuego");
        }

        [Test]
        public void CategoryMappingFollowsTheRequiredTable()
        {
            var m = new GameConfig().CategoryByColor;
            Assert.AreEqual(Category.FrutasYVerduras, m[TileColor.Naranja]);
            Assert.AreEqual(Category.PartesDelCuerpo, m[TileColor.Rojo]);
            Assert.AreEqual(Category.AccesoriosDeRopa, m[TileColor.Amarillo]);
            Assert.AreEqual(Category.Animales, m[TileColor.Verde]);
            Assert.AreEqual(Category.Profesiones, m[TileColor.Azul]);
            Assert.AreEqual(Category.Deportes, m[TileColor.Morado]);
            Assert.AreEqual(Category.Astronomia, m[TileColor.Negro]);
        }
    }

    public class EvaluatorTests
    {
        static FigureDefinition Fig() { return Fixtures.Figure("f", Category.Animales, Difficulty.Facil, 0, 1, 1, -1); }

        static PixelGrid Grid(params int[] c)
        {
            var g = new PixelGrid(2, 2);
            for (int i = 0; i < 4; i++) if (c[i] >= 0) g.SetInternal(i % 2, i / 2, c[i]);
            return g;
        }

        [Test] public void PerfectMatchScoresOneHundred()
        {
            var r = FigureEvaluator.Evaluate(Fig(), Grid(0, 1, 1, -1), new ScoreWeights());
            Assert.AreEqual(100.0, r.Score, 1e-9); Assert.AreEqual(3, r.Correct); Assert.AreEqual(0, r.Wrong); Assert.AreEqual(0, r.Missing);
            Assert.AreEqual(1.0, r.Similarity, 1e-9);
        }

        [Test] public void EmptyGridScoresZeroAndAllMissing()
        {
            var r = FigureEvaluator.Evaluate(Fig(), Grid(-1, -1, -1, -1), new ScoreWeights());
            Assert.AreEqual(0.0, r.Score); Assert.AreEqual(3, r.Missing);
        }

        [Test] public void WrongColorAndExtraPixelCountAsWrongNotMissing()
        {
            var r = FigureEvaluator.Evaluate(Fig(), Grid(2, 1, -1, 5), new ScoreWeights());
            Assert.AreEqual(1, r.Correct); Assert.AreEqual(2, r.Wrong); Assert.AreEqual(1, r.Missing);
        }

        [Test] public void ScoreIsReproducibleAndNeverNegative()
        {
            var g = Grid(5, 5, 5, 5);
            var a = FigureEvaluator.Evaluate(Fig(), g, new ScoreWeights());
            var b = FigureEvaluator.Evaluate(Fig(), g, new ScoreWeights());
            Assert.AreEqual(a.Score, b.Score); Assert.AreEqual(0.0, a.Score);
        }

        [Test] public void WeightsAreConfigurable()
        {
            var g = Grid(0, -1, -1, -1);
            double lenient = FigureEvaluator.Evaluate(Fig(), g, new ScoreWeights { Missing = 0.0 }).Score;
            double strict = FigureEvaluator.Evaluate(Fig(), g, new ScoreWeights { Missing = 1.0 }).Score;
            Assert.Greater(lenient, strict);
        }

        [Test] public void DimensionMismatchThrows()
        {
            Assert.Throws<ArgumentException>(() => FigureEvaluator.Evaluate(Fig(), new PixelGrid(3, 3), new ScoreWeights()));
        }
    }

    public class MinigameTests
    {
        static MinigameRound Round(out PlayerState a, out PlayerState b, float penaltyForB = 0f)
        {
            var cfg = new GameConfig();
            a = new PlayerState { Id = 0, Inventory = new Inventory(8, 200) };
            b = new PlayerState { Id = 1, Inventory = new Inventory(8, 200), PendingTimePenalty = penaltyForB };
            return new MinigameRound(Fixtures.Figure("f", Category.Animales, Difficulty.Facil, 0, 1, 1, -1), Category.Animales,
                new[] { a, b }, cfg.MinigameSeconds, 8);
        }

        [Test] public void EachPlayerHasAnIndependentGrid()
        {
            PlayerState a, b; var r = Round(out a, out b);
            Assert.AreEqual(PaintResult.Ok, r.TryPaint(0, 0, 0, 3));
            Assert.AreEqual(3, r.GridOf(0).Get(0, 0));
            Assert.AreEqual(PixelGrid.Empty, r.GridOf(1).Get(0, 0));
            Assert.AreEqual(PaintResult.NotParticipant, r.TryPaint(7, 0, 0, 3));
        }

        [Test] public void PaintChangeEraseKeepInventoryConsistent()
        {
            PlayerState a, b; var r = Round(out a, out b);
            r.TryPaint(0, 0, 0, 3);
            Assert.AreEqual(199, a.Inventory.Get(3));
            r.TryPaint(0, 0, 0, 3);                      // mismo color: no cuesta de nuevo
            Assert.AreEqual(199, a.Inventory.Get(3));
            r.TryPaint(0, 0, 0, 4);                      // cambiar devuelve la unidad anterior
            Assert.AreEqual(200, a.Inventory.Get(3)); Assert.AreEqual(199, a.Inventory.Get(4));
            r.TryErase(0, 0, 0);                         // borrar devuelve la unidad
            Assert.AreEqual(200, a.Inventory.Get(4));
            Assert.AreEqual(PaintResult.NoChange, r.TryErase(0, 0, 0));
        }

        [Test] public void CannotPaintWithoutUnitsOrWithInvalidColor()
        {
            PlayerState a, b; var r = Round(out a, out b);
            a.Inventory.TryTake(2, 200);
            Assert.AreEqual(PaintResult.NoUnits, r.TryPaint(0, 0, 0, 2));
            Assert.AreEqual(0, a.Inventory.Get(2));
            Assert.AreEqual(PaintResult.InvalidColor, r.TryPaint(0, 0, 0, 99));
            Assert.AreEqual(PaintResult.OutOfBounds, r.TryPaint(0, 5, 5, 1));
        }

        [Test] public void PressFinishEndsTheRoundForEveryone()
        {
            PlayerState a, b; var r = Round(out a, out b);
            Assert.IsTrue(r.PressFinish(1));
            Assert.IsTrue(r.Finished); Assert.AreEqual(FinishReason.PlayerPressed, r.Reason); Assert.AreEqual(1, r.FinishedByPlayerId);
            Assert.AreEqual(PaintResult.Finished, r.TryPaint(0, 0, 0, 1));
            Assert.AreEqual(PaintResult.Finished, r.TryPaint(1, 0, 0, 1));
            Assert.IsFalse(r.PressFinish(0), "solo hay un evento de fin");
        }

        [Test] public void TimerEndsTheRoundAtNinetySeconds()
        {
            PlayerState a, b; var r = Round(out a, out b);
            r.Tick(89f); Assert.IsFalse(r.Finished); Assert.AreEqual(1f, r.TimeRemaining, 1e-4f);
            r.Tick(2f); Assert.IsTrue(r.Finished); Assert.AreEqual(FinishReason.Timeout, r.Reason); Assert.AreEqual(0f, r.TimeRemaining);
        }

        [Test] public void TimePenaltyLocksOnlyThatPlayerAtTheStart()
        {
            PlayerState a, b; var r = Round(out a, out b, 10f);
            Assert.AreEqual(PaintResult.Locked, r.TryPaint(1, 0, 0, 1));
            Assert.AreEqual(PaintResult.Ok, r.TryPaint(0, 0, 0, 1));
            r.Tick(10f);
            Assert.AreEqual(PaintResult.Ok, r.TryPaint(1, 0, 0, 1));
        }
    }

    public class RobberyAndRulesTests
    {
        static MinigameRound RoundWithGrids(PlayerState[] ps, int[][] cells)
        {
            var fig = new FigureDefinition { Id = "f", Name = "f", Category = Category.Animales, Difficulty = Difficulty.Facil, Width = 3, Height = 2, Cells = new[] { 0, 0, 1, 2, -1, 3 } };
            var r = new MinigameRound(fig, Category.Animales, ps, 90f, 8);
            for (int p = 0; p < ps.Length; p++)
                for (int i = 0; i < 6; i++)
                    if (cells[p][i] >= 0) r.TryPaint(ps[p].Id, i % 3, i / 3, cells[p][i]);
            return r;
        }

        static Dictionary<int, PlayerState> Map(params PlayerState[] ps)
        {
            var d = new Dictionary<int, PlayerState>(); foreach (var p in ps) d[p.Id] = p; return d;
        }

        static IDictionary<int, IDictionary<int, int>> Sel(int winner, int opponent, int row)
        {
            return new Dictionary<int, IDictionary<int, int>> { { winner, new Dictionary<int, int> { { opponent, row } } } };
        }

        [Test] public void WinnerRecoversColorsOfTheChosenRowAndEmptyCellsGiveNothing()
        {
            var w = new PlayerState { Id = 0, Inventory = new Inventory(8, 200) };
            var o = new PlayerState { Id = 1, Inventory = new Inventory(8, 200) };
            var r = RoundWithGrids(new[] { w, o }, new[] { new[] { -1, -1, -1, -1, -1, -1 }, new[] { 0, 0, 1, 2, -1, 3 } });
            int before = w.Inventory.Total;
            var t = RowRobbery.Resolve(new[] { 0 }, Map(w, o), r, Sel(0, 1, 0), 8);
            Assert.AreEqual(1, t.Count);
            Assert.AreEqual(2, t[0].Units[0]); Assert.AreEqual(1, t[0].Units[1]);
            Assert.AreEqual(before + 3, w.Inventory.Total, "solo suma pixeles reales de la fila");
            Assert.AreEqual(PixelGrid.Empty, r.GridOf(1).Get(0, 0), "la fila robada se vacia");
            var emptyRow = RowRobbery.Resolve(new[] { 0 }, Map(w, o), r, Sel(0, 1, 1), 8);
            Assert.AreEqual(2, TotalUnits(emptyRow[0]), "fila con un hueco: solo 2 pixeles");
        }

        static int TotalUnits(RobberyTransfer t) { int s = 0; foreach (var u in t.Units) s += u; return s; }

        [Test] public void TiedWinnersSplitFlooredAndNeverCreateResources()
        {
            var w1 = new PlayerState { Id = 0, Inventory = new Inventory(8, 200) };
            var w2 = new PlayerState { Id = 1, Inventory = new Inventory(8, 200) };
            var o = new PlayerState { Id = 2, Inventory = new Inventory(8, 200) };
            var r = RoundWithGrids(new[] { w1, w2, o }, new[] { new[] { -1, -1, -1, -1, -1, -1 }, new[] { -1, -1, -1, -1, -1, -1 }, new[] { 0, 0, 1, 2, -1, 3 } });
            var sel = new Dictionary<int, IDictionary<int, int>>
            {
                { 0, new Dictionary<int, int> { { 2, 0 } } },
                { 1, new Dictionary<int, int> { { 2, 0 } } },
            };
            int total = w1.Inventory.Total + w2.Inventory.Total;
            RowRobbery.Resolve(new[] { 0, 1 }, Map(w1, w2, o), r, sel, 8);
            // fila 0 = {0:2, 1:1}; a cada ganador le tocan floor(2/2)=1 de color 0 y floor(1/2)=0 de color 1.
            Assert.AreEqual(201, w1.Inventory.Get(0)); Assert.AreEqual(201, w2.Inventory.Get(0));
            Assert.AreEqual(200, w1.Inventory.Get(1));
            Assert.LessOrEqual(w1.Inventory.Total + w2.Inventory.Total - total, 3, "no se crean recursos");
        }

        [Test] public void InvalidSelectionsAreRejected()
        {
            var w = new PlayerState { Id = 0, Inventory = new Inventory(8, 200) };
            var o = new PlayerState { Id = 1, Inventory = new Inventory(8, 200) };
            var r = RoundWithGrids(new[] { w, o }, new[] { new[] { -1, -1, -1, -1, -1, -1 }, new[] { 0, 0, 1, 2, -1, 3 } });
            Assert.Throws<InvalidOperationException>(() => RowRobbery.Resolve(new[] { 0 }, Map(w, o), r, Sel(0, 1, 9), 8));
            Assert.Throws<InvalidOperationException>(() => RowRobbery.Resolve(new[] { 0 }, Map(w, o), r, new Dictionary<int, IDictionary<int, int>>(), 8));
        }

        [Test] public void PlayerWhoCannotBuildAnyFigureIsDisqualified()
        {
            var catalog = Fixtures.Catalog();                 // cada figura necesita colores 0 y 1
            var p = new PlayerState { Id = 0, Inventory = new Inventory(8, 200) };
            var q = new PlayerState { Id = 1, Inventory = new Inventory(8, 200) };
            Assert.AreEqual(0, Rules.UpdateDisqualifications(new[] { p, q }, catalog, 8).Count);
            p.Inventory.TryTake(1, 199);                      // le queda 1 unidad de color 1: aun puede (necesita 2)? no: la figura necesita 2
            var newly = Rules.UpdateDisqualifications(new[] { p, q }, catalog, 8);
            Assert.AreEqual(1, newly.Count); Assert.IsTrue(p.Disqualified); Assert.IsFalse(q.Disqualified);
        }

        [Test] public void LowTotalButBuildableFigureIsNotDisqualified()
        {
            var catalog = Fixtures.Catalog();
            var p = new PlayerState { Id = 0, Inventory = new Inventory(8, 0) };
            p.Inventory.Add(0, 1); p.Inventory.Add(1, 2);     // total 3, justo lo que pide la figura
            Assert.AreEqual(0, Rules.UpdateDisqualifications(new[] { p }, catalog, 8).Count);
        }

        [Test] public void DisqualifiedPlayerStaysDisqualifiedAfterReceivingResources()
        {
            var catalog = Fixtures.Catalog();
            var p = new PlayerState { Id = 0, Inventory = new Inventory(8, 0) };
            Rules.UpdateDisqualifications(new[] { p }, catalog, 8);
            Assert.IsTrue(p.Disqualified);
            p.Inventory.Add(0, 100); p.Inventory.Add(1, 100);
            Rules.UpdateDisqualifications(new[] { p }, catalog, 8);
            Assert.IsTrue(p.Disqualified);
        }

        [Test] public void EliminationVictoryAndNoWinnerCase()
        {
            var a = new PlayerState { Id = 0 }; var b = new PlayerState { Id = 1 };
            Assert.AreEqual(OutcomeKind.None, VictoryChecker.CheckElimination(new[] { a, b }).Kind);
            b.Disqualified = true;
            var o = VictoryChecker.CheckElimination(new[] { a, b });
            Assert.AreEqual(OutcomeKind.WinByElimination, o.Kind); Assert.AreEqual(0, o.WinnerId);
            a.Disqualified = true;
            Assert.AreEqual(OutcomeKind.NoWinner, VictoryChecker.CheckElimination(new[] { a, b }).Kind);
        }

        [Test] public void InventoryNeverGoesNegative()
        {
            var inv = new Inventory(3, 5);
            Assert.IsFalse(inv.TryTake(0, 6)); Assert.AreEqual(5, inv.Get(0));
            Assert.IsTrue(inv.TryTake(0, 5)); Assert.IsFalse(inv.TryTake(0, 1));
            Assert.Throws<ArgumentException>(() => inv.Add(0, -1));
        }
    }

    public class SessionFlowTests
    {
        [Test]
        public void FullRoundMinigameRobberyAndContinuation()
        {
            // Orden: J0=2, J1=6 -> J1 primero. J1 saca Avanzar 1 -> casilla 1 (Rojo, Partes del cuerpo) -> minijuego.
            // Pick(categoria) usa Next(0,1)=0.
            var s = Fixtures.Session(new QueueRandom(2, 6, 0, 0));
            var events = new List<GameEventType>();
            s.Event += e => events.Add(e.Type);
            Fixtures.StartAndRollOrder(s); s.RollMoveDie();
            Assert.AreEqual(GamePhase.MinigameRunning, s.Phase);

            // J0 dibuja la figura perfecta (0,1 / 1,-); J1 se queda a medias.
            var fig = s.Round.Figure;
            for (int y = 0; y < fig.Height; y++)
                for (int x = 0; x < fig.Width; x++)
                    if (fig.CellAt(x, y) >= 0) s.Round.TryPaint(0, x, y, fig.CellAt(x, y));
            s.Round.TryPaint(1, 0, 0, fig.CellAt(0, 0));

            Assert.IsTrue(s.PressFinish(1));      // el jugador que activo el minijuego tambien puede terminarlo
            Assert.AreEqual(GamePhase.AwaitingRowSelection, s.Phase);
            CollectionAssert.AreEqual(new[] { 0 }, new List<int>(s.RoundWinners));
            Assert.AreEqual(100.0, s.LastEvaluation[0].Score, 1e-9);
            Assert.Less(s.LastEvaluation[1].Score, 100.0);

            int before = s.GetPlayer(0).Inventory.Total;
            var sel = new Dictionary<int, IDictionary<int, int>> { { 0, new Dictionary<int, int> { { 1, 0 } } } };
            var transfers = s.ResolveRewards(sel);
            Assert.AreEqual(1, transfers.Count);
            Assert.AreEqual(before + 1, s.GetPlayer(0).Inventory.Total, "J1 solo pinto 1 pixel en la fila 0");
            Assert.AreEqual(GamePhase.AwaitingMoveRoll, s.Phase);
            Assert.AreEqual(0, s.CurrentPlayer.Id, "turno siguiente");
            CollectionAssert.Contains(events, GameEventType.RewardsApplied);
        }

        [Test]
        public void EverybodyEmptyMeansNoMinigameWinnerAndNobodyStealsAnything()
        {
            var s = Fixtures.Session(new QueueRandom(2, 6, 0, 0));
            Fixtures.StartAndRollOrder(s); s.RollMoveDie();
            s.Tick(200f);                          // el temporizador termina el desafio
            Assert.AreEqual(GamePhase.AwaitingMoveRoll, s.Phase);
            Assert.AreEqual(0, s.RoundWinners.Count);
        }

        [Test]
        public void TiedMinigameResultSharesTheReward()
        {
            // 3 jugadores: orden J0=2, J1=6, J2=4 -> empieza J1, que saca Avanzar 1 y cae en una casilla roja.
            var s = Fixtures.Session(new QueueRandom(2, 6, 4, 0, 0), 3);
            Fixtures.StartAndRollOrder(s); s.RollMoveDie();
            var fig = s.Round.Figure;
            foreach (int pid in new[] { 0, 1 })
                for (int y = 0; y < fig.Height; y++)
                    for (int x = 0; x < fig.Width; x++)
                        if (fig.CellAt(x, y) >= 0) s.Round.TryPaint(pid, x, y, fig.CellAt(x, y));
            s.PressFinish(2);
            CollectionAssert.AreEqual(new[] { 0, 1 }, new List<int>(s.RoundWinners));
        }

        [Test]
        public void GameEndsWhenEveryoneButOneIsDisqualified()
        {
            var s = Fixtures.Session(new QueueRandom(2, 6, 0, 0));
            Fixtures.StartAndRollOrder(s); s.RollMoveDie();
            // J1 se queda sin color 1: tras este minijuego ya no puede construir ninguna figura del catalogo.
            s.GetPlayer(1).Inventory.TryTake(1, 200);
            s.PressFinish(0);
            Assert.AreEqual(GamePhase.GameOver, s.Phase);
            Assert.AreEqual(OutcomeKind.WinByElimination, s.Outcome.Kind);
            Assert.AreEqual(0, s.Outcome.WinnerId);
        }

        [Test]
        public void EveryoneDisqualifiedAtOnceHasNoWinner()
        {
            var s = Fixtures.Session(new QueueRandom(2, 6, 0, 0));
            Fixtures.StartAndRollOrder(s); s.RollMoveDie();
            s.GetPlayer(0).Inventory.TryTake(1, 200); s.GetPlayer(1).Inventory.TryTake(1, 200);
            s.PressFinish(0);
            Assert.AreEqual(OutcomeKind.NoWinner, s.Outcome.Kind);
            Assert.AreEqual(-1, s.Outcome.WinnerId);
        }

        [Test]
        public void CharactersCannotBeRepeatedAndMustBeChosenBeforeStarting()
        {
            var s = new GameSession(new GameConfig(), Fixtures.SmallBoard(), Fixtures.Catalog(), new QueueRandom(1));
            s.AddPlayer("A", 0);
            Assert.Throws<InvalidOperationException>(() => s.AddPlayer("B", 0));
            Assert.Throws<InvalidOperationException>(() => s.StartGame(), "hacen falta al menos dos jugadores");
        }
    }
}
