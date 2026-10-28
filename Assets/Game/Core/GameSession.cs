using System;
using System.Collections.Generic;

namespace RobaColores.Game.Core
{
    public enum GamePhase
    {
        Setup,
        AwaitingMoveRoll,
        AwaitingPathChoice,
        MinigameRunning,
        AwaitingRowSelection,
        GameOver,
    }

    public enum GameEventType
    {
        OrderDetermined, TurnStarted, TurnSkipped, MoveRolled, TimePenaltyApplied, PathChoiceRequired, PlayerMoved,
        MinigameStarted, MinigameFinished, RowSelectionRequired, RewardsApplied, NoWinnerInMinigame, PlayerDisqualified, GameOver,
    }

    public sealed class GameEvent
    {
        public GameEventType Type;
        public int PlayerId = -1;
        public int A;
        public int B;
        public string Text;
        public override string ToString() { return Type + " p=" + PlayerId + " a=" + A + " b=" + B + (Text != null ? " " + Text : ""); }
    }

    // Orquesta una partida completa. Es C# puro: no depende de Unity, de escenas ni de la red.
    // La capa de presentacion llama a estos metodos y escucha Event. Cada metodo valida la fase actual.
    public sealed class GameSession
    {
        public GameConfig Config { get; private set; }
        public BoardGraph Board { get; private set; }
        public FigureCatalog Catalog { get; private set; }
        readonly IRandom rng;
        readonly InitialDie initialDie = new InitialDie();
        readonly MoveDie moveDie;

        readonly List<PlayerState> players = new List<PlayerState>();
        readonly Dictionary<int, PlayerState> playersById = new Dictionary<int, PlayerState>();
        readonly List<GameEvent> log = new List<GameEvent>();
        List<int> turnOrder = new List<int>();
        int currentIndex;
        int pendingSteps;
        List<int> winnersOfRound = new List<int>();
        Dictionary<int, EvaluationResult> lastEvaluation;

        public event Action<GameEvent> Event;

        public GamePhase Phase { get; private set; }
        public IList<PlayerState> Players { get { return players; } }
        public IList<int> TurnOrder { get { return turnOrder; } }
        public TurnOrderResult OrderResult { get; private set; }
        public IList<GameEvent> Log { get { return log; } }
        public MinigameRound Round { get; private set; }
        public Dictionary<int, EvaluationResult> LastEvaluation { get { return lastEvaluation; } }
        public IList<int> RoundWinners { get { return winnersOfRound; } }
        public IList<int> PathOptions { get; private set; }
        public MoveFace LastMoveFace { get; private set; }
        public GameOutcome Outcome { get; private set; }
        public PlayerState CurrentPlayer { get { return players.Count == 0 || turnOrder.Count == 0 ? null : playersById[turnOrder[currentIndex]]; } }

        public GameSession(GameConfig config, BoardGraph board, FigureCatalog catalog, IRandom random)
        {
            Config = config; Board = board; Catalog = catalog; rng = random;
            moveDie = new MoveDie(config.MoveDieFaces);
            Outcome = GameOutcome.Continue;
            PathOptions = new List<int>();
            Phase = GamePhase.Setup;
        }

        public PlayerState GetPlayer(int id) { return playersById[id]; }

        // ---------------- Preparacion ----------------

        // Cada jugador conserva su personaje toda la partida; no se pueden repetir personajes.
        public PlayerState AddPlayer(string name, int characterId)
        {
            Require(Phase == GamePhase.Setup, "Solo se pueden anadir jugadores antes de empezar.");
            if (players.Count >= Config.MaxPlayers) throw new InvalidOperationException("Maximo de jugadores alcanzado.");
            foreach (var p in players) if (p.CharacterId == characterId) throw new InvalidOperationException("Personaje ya elegido.");
            var player = new PlayerState
            {
                Id = players.Count,
                Name = name,
                CharacterId = characterId,
                Inventory = new Inventory(Config.PaletteSize, Config.InitialUnitsPerColor),
            };
            players.Add(player);
            playersById[player.Id] = player;
            return player;
        }

        public void StartGame()
        {
            Require(Phase == GamePhase.Setup, "La partida ya empezo.");
            if (players.Count < Config.MinPlayers) throw new InvalidOperationException("Faltan jugadores.");
            var problems = Board.Validate();
            if (problems.Count > 0) throw new InvalidOperationException("Tablero invalido: " + string.Join("; ", problems.ToArray()));
            if (Catalog.All.Count == 0) throw new InvalidOperationException("El catalogo de figuras esta vacio.");
            foreach (var f in Catalog.All) f.Validate(Config.PaletteSize);

            foreach (var p in players) p.TileId = Board.StartId;

            var ids = new List<int>();
            foreach (var p in players) ids.Add(p.Id);
            OrderResult = TurnOrderResolver.Resolve(ids, initialDie, rng);
            turnOrder = OrderResult.Order;
            currentIndex = 0;
            Emit(new GameEvent { Type = GameEventType.OrderDetermined, PlayerId = turnOrder[0] });
            BeginTurn();
        }

        // ---------------- Turnos y movimiento ----------------

        void BeginTurn()
        {
            if (CheckEliminationAndMaybeEnd()) return;
            for (int guard = 0; guard < players.Count * 3; guard++)
            {
                var p = CurrentPlayer;
                if (p.Disqualified) { currentIndex = (currentIndex + 1) % turnOrder.Count; continue; }
                if (p.SkipNextTurn)
                {
                    p.SkipNextTurn = false;   // el turno perdido se consume aqui, al empezar su turno
                    Emit(new GameEvent { Type = GameEventType.TurnSkipped, PlayerId = p.Id });
                    currentIndex = (currentIndex + 1) % turnOrder.Count;
                    continue;
                }
                Phase = GamePhase.AwaitingMoveRoll;
                Emit(new GameEvent { Type = GameEventType.TurnStarted, PlayerId = p.Id });
                return;
            }
            throw new InvalidOperationException("No hay jugador disponible para jugar el turno.");
        }

        void EndTurn()
        {
            currentIndex = (currentIndex + 1) % turnOrder.Count;
            BeginTurn();
        }

        public MoveFace RollMoveDie()
        {
            Require(Phase == GamePhase.AwaitingMoveRoll, "No es momento de tirar el dado de movimiento.");
            var p = CurrentPlayer;
            var face = moveDie.Roll(rng);
            LastMoveFace = face;
            Emit(new GameEvent { Type = GameEventType.MoveRolled, PlayerId = p.Id, A = (int)face.Kind, B = face.Steps });
            switch (face.Kind)
            {
                case MoveFaceKind.Advance:
                    pendingSteps = face.Steps;
                    AdvanceSteps();
                    break;
                case MoveFaceKind.LoseTurn:
                    p.SkipNextTurn = true;
                    EndTurn();
                    break;
                case MoveFaceKind.TimePenalty:
                    p.PendingTimePenalty = Config.TimePenaltySeconds;
                    Emit(new GameEvent { Type = GameEventType.TimePenaltyApplied, PlayerId = p.Id, A = (int)Config.TimePenaltySeconds });
                    EndTurn();
                    break;
            }
            return face;
        }

        // Avanza paso a paso hasta agotar los pasos, llegar a la meta o encontrar una bifurcacion.
        void AdvanceSteps()
        {
            var p = CurrentPlayer;
            while (pendingSteps > 0)
            {
                var tile = Board.Get(p.TileId);
                if (tile.IsFinish) break;
                if (tile.Next.Count == 0) { pendingSteps = 0; break; }
                if (tile.Next.Count > 1)
                {
                    PathOptions = new List<int>(tile.Next);
                    Phase = GamePhase.AwaitingPathChoice;
                    Emit(new GameEvent { Type = GameEventType.PathChoiceRequired, PlayerId = p.Id, A = pendingSteps });
                    return;
                }
                MoveTo(p, tile.Next[0]);
                pendingSteps--;
                if (Board.Get(p.TileId).IsFinish) break;
            }
            Landed();
        }

        // El jugador elige uno de los caminos validos de la bifurcacion en la que esta.
        public void ChoosePath(int tileId)
        {
            Require(Phase == GamePhase.AwaitingPathChoice, "No hay una bifurcacion pendiente.");
            if (!PathOptions.Contains(tileId)) throw new InvalidOperationException("Camino no valido desde esta casilla.");
            var p = CurrentPlayer;
            PathOptions = new List<int>();
            MoveTo(p, tileId);
            pendingSteps--;
            Phase = GamePhase.AwaitingMoveRoll;
            if (Board.Get(p.TileId).IsFinish) pendingSteps = 0;
            AdvanceSteps();
        }

        void MoveTo(PlayerState p, int tileId)
        {
            int from = p.TileId;
            p.TileId = tileId;
            Emit(new GameEvent { Type = GameEventType.PlayerMoved, PlayerId = p.Id, A = from, B = tileId });
        }

        void Landed()
        {
            var p = CurrentPlayer;
            var tile = Board.Get(p.TileId);
            if (tile.IsFinish)
            {
                Outcome = new GameOutcome { Kind = OutcomeKind.WinByFinish, WinnerId = p.Id };
                Phase = GamePhase.GameOver;
                Emit(new GameEvent { Type = GameEventType.GameOver, PlayerId = p.Id, A = (int)OutcomeKind.WinByFinish });
                return;
            }
            if (tile.TriggersMinigame) StartMinigame(tile);
            else EndTurn();
        }

        // ---------------- Minijuego ----------------

        void StartMinigame(BoardTile tile)
        {
            var category = Config.CategoryByColor[tile.Color];
            var figure = Catalog.Pick(category, tile.Difficulty, rng);
            var participants = new List<PlayerState>();
            foreach (var p in players) if (p.IsActive) participants.Add(p);
            Round = new MinigameRound(figure, category, participants, Config.MinigameSeconds, Config.PaletteSize);
            Phase = GamePhase.MinigameRunning;
            Emit(new GameEvent { Type = GameEventType.MinigameStarted, PlayerId = CurrentPlayer.Id, A = (int)category, Text = figure.Id });
        }

        public void Tick(float deltaSeconds)
        {
            if (Phase != GamePhase.MinigameRunning) return;
            Round.Tick(deltaSeconds);
            if (Round.Finished) OnMinigameFinished();
        }

        public bool PressFinish(int playerId)
        {
            if (Phase != GamePhase.MinigameRunning) return false;
            bool ok = Round.PressFinish(playerId);
            if (ok) OnMinigameFinished();
            return ok;
        }

        void OnMinigameFinished()
        {
            lastEvaluation = Round.Evaluate(Config.Score);
            Emit(new GameEvent { Type = GameEventType.MinigameFinished, PlayerId = Round.FinishedByPlayerId, A = (int)Round.Reason });

            double best = double.MinValue;
            foreach (var kv in lastEvaluation) if (kv.Value.Score > best) best = kv.Value.Score;
            winnersOfRound = new List<int>();
            if (best >= Config.MinWinningScore)
                foreach (var kv in lastEvaluation) if (Math.Abs(kv.Value.Score - best) < 1e-9) winnersOfRound.Add(kv.Key);
            winnersOfRound.Sort();   // sin desempate por orden de finalizacion

            if (winnersOfRound.Count == 0) Emit(new GameEvent { Type = GameEventType.NoWinnerInMinigame });
            if (winnersOfRound.Count == 0 || RowRobbery.Opponents(winnersOfRound, Round).Count == 0)
            {
                FinishResolution();
                return;
            }
            Phase = GamePhase.AwaitingRowSelection;
            Emit(new GameEvent { Type = GameEventType.RowSelectionRequired, A = winnersOfRound.Count });
        }

        // selecciones: ganador -> (oponente -> fila elegida).
        public List<RobberyTransfer> ResolveRewards(IDictionary<int, IDictionary<int, int>> selections)
        {
            Require(Phase == GamePhase.AwaitingRowSelection, "No hay un robo pendiente.");
            var byId = new Dictionary<int, PlayerState>(playersById);
            var transfers = RowRobbery.Resolve(winnersOfRound, byId, Round, selections, Config.PaletteSize);
            Emit(new GameEvent { Type = GameEventType.RewardsApplied, A = transfers.Count });
            FinishResolution();
            return transfers;
        }

        void FinishResolution()
        {
            var newly = Rules.UpdateDisqualifications(players, Catalog, Config.PaletteSize);
            foreach (var p in newly) Emit(new GameEvent { Type = GameEventType.PlayerDisqualified, PlayerId = p.Id });
            Round = null;
            if (CheckEliminationAndMaybeEnd()) return;
            EndTurn();
        }

        bool CheckEliminationAndMaybeEnd()
        {
            if (Phase == GamePhase.Setup || Phase == GamePhase.GameOver) return Phase == GamePhase.GameOver;
            var outcome = VictoryChecker.CheckElimination(players);
            if (outcome.Kind == OutcomeKind.None) return false;
            Outcome = outcome;
            Phase = GamePhase.GameOver;
            Emit(new GameEvent { Type = GameEventType.GameOver, PlayerId = outcome.WinnerId, A = (int)outcome.Kind });
            return true;
        }

        // ---------------- Utilidades ----------------

        void Emit(GameEvent e)
        {
            log.Add(e);
            var handler = Event;
            if (handler != null) handler(e);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
