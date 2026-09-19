using System;
using System.Collections.Generic;
using Godot;
using TicTacToeRoguelike.Application.AI;
using TicTacToeRoguelike.Application.Actions;
using TicTacToeRoguelike.Application.Encounters;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Combat;
using TicTacToeRoguelike.Domain.Moves;
using TicTacToeRoguelike.Domain.Reactions;
using TicTacToeRoguelike.Domain.Scoring;
using TicTacToeRoguelike.Presentation.Board;
using TicTacToeRoguelike.Presentation.Combat;

namespace TicTacToeRoguelike.Presentation.Encounters
{
    /// <summary>
    /// Composition root fino do Godot. Input humano e IA produzem GameAction e
    /// passam pelo mesmo ActionCatalog -> ActionExecutor -> EncounterEngine.
    /// </summary>
    public sealed partial class EncounterController : Control
    {
        private EncounterEngine _engine;
        private ActionCatalog _catalog;
        private BasicTicTacToePolicy _enemyPolicy;
        private BoardView _boardView;
        private CombatReportAnimator _combatAnimator;

        private Label _statusLabel;
        private Label _healthLabel;
        private Label _reactionLabel;
        private Button _nextRoundButton;

        private int _encounterGeneration;
        private bool _enemyTurnScheduled;

        public override void _Ready()
        {
            BuildInterface();
            ComposeEncounter();
            StartFirstRound();
        }

        private void BuildInterface()
        {
            MarginContainer margin = new MarginContainer();
            margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_left", 48);
            margin.AddThemeConstantOverride("margin_right", 48);
            margin.AddThemeConstantOverride("margin_top", 36);
            margin.AddThemeConstantOverride("margin_bottom", 36);
            AddChild(margin);

            VBoxContainer column = new VBoxContainer();
            column.AddThemeConstantOverride("separation", 18);
            margin.AddChild(column);

            Label title = new Label
            {
                Text = "TicTacToe Roguelike",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            title.AddThemeFontSizeOverride("font_size", 32);
            column.AddChild(title);

            _healthLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
            _statusLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
            _reactionLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };

            column.AddChild(_healthLabel);
            column.AddChild(_statusLabel);
            column.AddChild(_reactionLabel);

            CenterContainer boardCenter = new CenterContainer();
            boardCenter.SizeFlagsVertical = SizeFlags.ExpandFill;
            column.AddChild(boardCenter);

            _boardView = new BoardView();
            _boardView.CellActivated += OnCellActivated;
            boardCenter.AddChild(_boardView);

            _nextRoundButton = new Button
            {
                Text = "Próxima rodada",
                Visible = false,
                CustomMinimumSize = new Vector2(0, 54)
            };
            _nextRoundButton.Pressed += OnNextRoundPressed;
            column.AddChild(_nextRoundButton);

            _combatAnimator = new CombatReportAnimator();
            AddChild(_combatAnimator);
        }

        private void ComposeEncounter()
        {
            MoveValidator moveValidator = new MoveValidator();
            MoveService moveService = new MoveService(moveValidator);

            _catalog = ActionCatalog.CreateDefault(moveValidator, moveService);

            ActionExecutor executor = ActionExecutor.CreateWithCatalog(_catalog);
            ActionAvailabilityService availability =
                new ActionAvailabilityService(
                    moveValidator,
                    Array.Empty<IActionAvailabilityProvider>());

            CombatantState player =
                new CombatantState("player", ScoreActor.Player, 100);
            CombatantState enemy =
                new CombatantState("enemy:default", ScoreActor.Enemy, 100);

            EncounterRules rules =
                new EncounterRules(
                    requiredSequenceLength: 3,
                    minimumScoringSequenceLength: 2,
                    maximumScoringSequenceLength: 3,
                    victoryMultiplier: 1.5m);

            _engine = new EncounterEngine(
                player,
                enemy,
                rules,
                executor,
                availability,
                new ReactionStateFactory(),
                new ReactionRule(),
                new ScorePipeline(),
                new ClashResolver(),
                new DamageResolver(),
                new AlternatingEncounterTurnScheduler());

            _enemyPolicy = new BasicTicTacToePolicy(
                maximumSearchDepth: 10,
                maximumVisitedNodes: 100_000);
        }

        private void StartFirstRound()
        {
            _encounterGeneration++;
            _engine.StartEncounter(
                CreateDefaultBoard(),
                EncounterSide.Player);

            RefreshPresentation();
            ScheduleEnemyTurnIfNeeded();
        }

        private BoardState CreateDefaultBoard()
        {
            BoardDefinition definition =
                BoardDefinition.CreateRectangular(3, 3);

            BoardCoordinate center = new BoardCoordinate(1, 1);
            CellState goldenCenter = new CellState(
                center,
                CellMark.None,
                new[] { CellModifierId.Golden });

            return new BoardState(
                definition,
                new[] { goldenCenter });
        }

        private void OnCellActivated(BoardCoordinate coordinate)
        {
            if (!_engine.State.AcceptsActions ||
                _engine.State.CurrentActor != ScoreActor.Player)
                return;

            IReadOnlyList<GameAction> actions =
                _catalog.GetAvailableActions(
                    _engine.State.Board,
                    _engine.State.CurrentTurn,
                    GameActionOrigin.PlayerInput);

            GameAction selected = null;
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i] is PlaceMarkAction place &&
                    place.Target == coordinate)
                {
                    selected = place;
                    break;
                }
            }

            if (selected == null)
                return;

            _engine.ExecuteAction(selected);
            AfterAuthoritativeAction();
        }

        [Callable]
        private void RunEnemyTurn()
        {
            _enemyTurnScheduled = false;

            if (!_engine.State.AcceptsActions ||
                _engine.State.CurrentActor != ScoreActor.Enemy)
                return;

            IReadOnlyList<GameAction> actions =
                _catalog.GetAvailableActions(
                    _engine.State.Board,
                    _engine.State.CurrentTurn,
                    GameActionOrigin.AgentPolicy);

            AgentDecisionContext context =
                new AgentDecisionContext(
                    _engine.State.Board,
                    _engine.State.CurrentTurn,
                    _engine.State.ReactionState,
                    _engine.State.Rules.RequiredSequenceLength,
                    actions,
                    _encounterGeneration);

            if (!_enemyPolicy.TryChooseAction(context, out GameAction selected))
            {
                _engine.SkipCurrentTurn();
                RefreshPresentation();
                ScheduleEnemyTurnIfNeeded();
                return;
            }

            if (!context.ContainsLegalAction(selected))
                throw new InvalidOperationException("A IA devolveu uma ação fora de LegalActions.");

            _engine.ExecuteAction(selected);
            AfterAuthoritativeAction();
        }

        private void AfterAuthoritativeAction()
        {
            if (_engine.State.Phase == EncounterPhase.RoundResolved)
            {
                _combatAnimator.Present(_engine.State.LastRoundResolution);
                _engine.AcknowledgeRoundResolution();
            }

            RefreshPresentation();
            ScheduleEnemyTurnIfNeeded();
        }

        private void ScheduleEnemyTurnIfNeeded()
        {
            if (_enemyTurnScheduled)
                return;

            if (_engine.State.AcceptsActions &&
                _engine.State.CurrentActor == ScoreActor.Enemy)
            {
                _enemyTurnScheduled = true;
                CallDeferred(nameof(RunEnemyTurn));
            }
        }

        private void OnNextRoundPressed()
        {
            if (_engine.State.Phase != EncounterPhase.WaitingForNextRound)
                return;

            _encounterGeneration++;
            _nextRoundButton.Visible = false;

            _engine.StartNextRound(
                CreateDefaultBoard(),
                EncounterSide.Player);

            RefreshPresentation();
            ScheduleEnemyTurnIfNeeded();
        }

        private void RefreshPresentation()
        {
            EncounterState state = _engine.State;

            bool playerCanClick =
                state.AcceptsActions &&
                state.CurrentActor == ScoreActor.Player;

            if (state.Board != null)
                _boardView.RenderBoard(state.Board, playerCanClick);

            _healthLabel.Text =
                $"Jogador: {state.PlayerState.CurrentHealth}/{state.PlayerState.MaximumHealth}   " +
                $"Inimigo: {state.EnemyState.CurrentHealth}/{state.EnemyState.MaximumHealth}";

            _statusLabel.Text = BuildStatusText(state);

            if (state.ReactionState == null)
            {
                _reactionLabel.Text = "";
            }
            else if (state.ReactionState.IsReactionActive)
            {
                _reactionLabel.Text =
                    $"Reação ativa — líder: {state.ReactionState.Leader}; " +
                    $"deve reagir: {state.ReactionState.PendingResponder}";
            }
            else
            {
                _reactionLabel.Text =
                    $"Sequências X/O: {state.ReactionState.PlayerSequenceCount}/" +
                    $"{state.ReactionState.EnemySequenceCount}";
            }

            _nextRoundButton.Visible =
                state.Phase == EncounterPhase.WaitingForNextRound;
        }

        private static string BuildStatusText(EncounterState state)
        {
            if (state.Phase == EncounterPhase.EncounterEnded)
            {
                return state.LastEncounterResult == null
                    ? "Encontro encerrado"
                    : $"Encontro encerrado — {state.LastEncounterResult.Outcome}";
            }

            if (state.Phase == EncounterPhase.WaitingForNextRound)
                return $"Rodada {state.RoundNumber} resolvida";

            if (state.AcceptsActions)
            {
                return state.CurrentActor == ScoreActor.Player
                    ? $"Rodada {state.RoundNumber} — sua vez (X)"
                    : $"Rodada {state.RoundNumber} — IA pensando (O)";
            }

            return $"Rodada {state.RoundNumber} — {state.Phase}";
        }
    }
}
