using System;
using System.Collections.Generic;
using System.Globalization;
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
using TicTacToeRoguelike.Presentation.Arena;
using TicTacToeRoguelike.Presentation.Board;
using TicTacToeRoguelike.Presentation.Combat;

namespace TicTacToeRoguelike.Presentation.Encounters
{
    /// <summary>
    /// Composition root do encontro e da arena mística.
    /// A apresentação usa somente estado e relatórios já resolvidos pelo domínio.
    /// </summary>
    public sealed partial class EncounterController : Control
    {
        private static readonly Color Bronze = new Color(0.52f, 0.34f, 0.17f, 0.82f);
        private static readonly Color BronzeSoft = new Color(0.40f, 0.27f, 0.15f, 0.52f);
        private static readonly Color Ivory = new Color(0.82f, 0.79f, 0.72f, 1f);
        private static readonly Color Muted = new Color(0.56f, 0.57f, 0.56f, 1f);
        private static readonly Color Cyan = new Color(0.08f, 0.82f, 0.84f, 1f);
        private static readonly Color Rose = new Color(1.00f, 0.31f, 0.40f, 1f);
        private static readonly Color PanelDark = new Color(0.018f, 0.022f, 0.026f, 0.94f);

        private const double ScoreSideDelay = 0.24;

        private EncounterEngine _engine;
        private ActionCatalog _catalog;
        private BasicTicTacToePolicy _enemyPolicy;
        private BoardView _boardView;
        private CombatReportAnimator _combatAnimator;

        private Label _roundLabel;
        private Label _turnLabel;
        private Label _playerHpText;
        private Label _enemyHpText;
        private Label _playerScoreLabel;
        private Label _enemyScoreLabel;
        private Label _playerMultiplierLabel;
        private Label _enemyMultiplierLabel;
        private Label _playerScoreDetailLabel;
        private Label _enemyScoreDetailLabel;

        private ProgressBar _playerHpBar;
        private ProgressBar _enemyHpBar;
        private Button _nextRoundButton;

        private int _encounterGeneration;
        private bool _enemyTurnScheduled;

        private bool _scoreAnimationActive;
        private RoundResolution _animatedResolution;
        private int _scoreAnimationSide;
        private int _scoreAnimationStep;
        private double _scoreStepElapsed;

        public override void _Ready()
        {
            BuildInterface();
            ComposeEncounter();
            StartFirstRound();
            SetProcess(false);
        }

        public override void _Process(double delta)
        {
            if (!_scoreAnimationActive || _animatedResolution == null)
            {
                SetProcess(false);
                return;
            }

            if (_scoreStepElapsed < 0d)
            {
                _scoreStepElapsed += delta;
                return;
            }

            ScorePipelineResult result = _scoreAnimationSide == 0
                ? _animatedResolution.PlayerScore
                : _animatedResolution.EnemyScore;

            Label scoreLabel = _scoreAnimationSide == 0
                ? _playerScoreLabel
                : _enemyScoreLabel;

            Label multiplierLabel = _scoreAnimationSide == 0
                ? _playerMultiplierLabel
                : _enemyMultiplierLabel;

            Label detailLabel = _scoreAnimationSide == 0
                ? _playerScoreDetailLabel
                : _enemyScoreDetailLabel;

            IReadOnlyList<ScoreStep> steps = result.Breakdown.Steps;

            if (_scoreAnimationStep >= steps.Count)
            {
                scoreLabel.Text =
                    result.Breakdown.FinalScore.ToString(CultureInfo.InvariantCulture);
                multiplierLabel.Text =
                    $"MULT × {FormatMultiplier(result.Breakdown.TotalMultiplier)}";
                detailLabel.Text = steps.Count == 0 ? "SEM PONTOS" : "TOTAL";

                AdvanceScoreAnimationSide();
                return;
            }

            ScoreStep step = steps[_scoreAnimationStep];
            double duration = GetScoreStepDuration(steps.Count);
            _scoreStepElapsed += delta;

            double progress = Math.Clamp(
                _scoreStepElapsed / duration,
                0d,
                1d);

            double eased = 1d - Math.Pow(1d - progress, 3d);
            double before = (double)step.ScoreBefore;
            double after = (double)step.ScoreAfter;
            double visibleScore = before + ((after - before) * eased);

            scoreLabel.Text = FormatAnimatedScore(visibleScore);
            detailLabel.Text = BuildScoreStepText(step);

            if (progress < 1d)
                return;

            _scoreAnimationStep++;
            _scoreStepElapsed = -0.07d;

            decimal visibleMultiplier = CalculateVisibleMultiplier(
                result.Breakdown,
                _scoreAnimationStep);

            multiplierLabel.Text =
                $"MULT × {FormatMultiplier(visibleMultiplier)}";
        }

        private void BuildInterface()
        {
            MysticArenaBackdrop background = new MysticArenaBackdrop();
            background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(background);

            Control layout = new Control();
            layout.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            layout.MouseFilter = MouseFilterEnum.Pass;
            AddChild(layout);

            PanelContainer opponentRunes = CreateRunePanel("RUNAS DO OPONENTE");
            Place(opponentRunes, 0.055f, 0.050f, 0.340f, 0.205f);
            layout.AddChild(opponentRunes);

            PanelContainer roundPanel = CreateRoundPanel();
            Place(roundPanel, 0.410f, 0.035f, 0.590f, 0.165f);
            layout.AddChild(roundPanel);

            PanelContainer opponentHp = CreateHealthPanel(
                "HP OPONENTE",
                Rose,
                out _enemyHpBar,
                out _enemyHpText);
            Place(opponentHp, 0.680f, 0.050f, 0.950f, 0.170f);
            layout.AddChild(opponentHp);

            PanelContainer boardPanel = CreateBoardPanel();
            Place(boardPanel, 0.350f, 0.190f, 0.660f, 0.720f);
            layout.AddChild(boardPanel);

            PanelContainer playerScore = CreateScorePanel(
                Cyan,
                out _playerScoreLabel,
                out _playerMultiplierLabel,
                out _playerScoreDetailLabel);
            Place(playerScore, 0.750f, 0.225f, 0.915f, 0.430f);
            layout.AddChild(playerScore);

            PanelContainer enemyScore = CreateScorePanel(
                Rose,
                out _enemyScoreLabel,
                out _enemyMultiplierLabel,
                out _enemyScoreDetailLabel);
            Place(enemyScore, 0.750f, 0.465f, 0.915f, 0.670f);
            layout.AddChild(enemyScore);

            PanelContainer playerRunes = CreateRunePanel("RUNAS DO JOGADOR");
            Place(playerRunes, 0.055f, 0.745f, 0.430f, 0.930f);
            layout.AddChild(playerRunes);

            PanelContainer playerHp = CreateHealthPanel(
                "HP JOGADOR",
                Cyan,
                out _playerHpBar,
                out _playerHpText);
            Place(playerHp, 0.650f, 0.775f, 0.950f, 0.910f);
            layout.AddChild(playerHp);

            _combatAnimator = new CombatReportAnimator();
            AddChild(_combatAnimator);
        }

        private PanelContainer CreateRunePanel(string title)
        {
            PanelContainer panel = CreateFramedPanel();
            MarginContainer margin = AddPadding(panel, 18, 12);

            VBoxContainer column = new VBoxContainer();
            column.AddThemeConstantOverride("separation", 8);
            margin.AddChild(column);

            Label heading = CreateHeading(title, 20, Ivory);
            column.AddChild(heading);
            column.AddChild(CreateDivider());

            Control emptyRuneArea = new Control
            {
                SizeFlagsVertical = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore
            };
            column.AddChild(emptyRuneArea);

            return panel;
        }

        private PanelContainer CreateRoundPanel()
        {
            PanelContainer panel = CreateFramedPanel();
            MarginContainer margin = AddPadding(panel, 14, 8);

            VBoxContainer column = new VBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center
            };
            column.AddThemeConstantOverride("separation", 2);
            margin.AddChild(column);

            _roundLabel = CreateHeading("RODADA 01", 28, Ivory);
            column.AddChild(_roundLabel);

            _turnLabel = CreateHeading("SEU TURNO", 20, Cyan);
            column.AddChild(_turnLabel);

            _nextRoundButton = new Button
            {
                Text = "PRÓXIMA RODADA",
                Visible = false,
                FocusMode = FocusModeEnum.None,
                CustomMinimumSize = new Vector2(0f, 34f)
            };
            StyleButton(_nextRoundButton);
            _nextRoundButton.Pressed += OnNextRoundPressed;
            column.AddChild(_nextRoundButton);

            return panel;
        }

        private PanelContainer CreateHealthPanel(
            string title,
            Color accent,
            out ProgressBar bar,
            out Label value)
        {
            PanelContainer panel = CreateFramedPanel();
            MarginContainer margin = AddPadding(panel, 18, 12);

            VBoxContainer column = new VBoxContainer();
            column.AddThemeConstantOverride("separation", 7);
            margin.AddChild(column);

            Label heading = new Label
            {
                Text = title,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            heading.AddThemeFontSizeOverride("font_size", 18);
            heading.AddThemeColorOverride("font_color", Ivory);
            column.AddChild(heading);

            HBoxContainer row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 16);
            column.AddChild(row);

            bar = CreateHealthBar(accent);
            bar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(bar);

            value = new Label
            {
                Text = "100 / 100",
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                CustomMinimumSize = new Vector2(96f, 28f)
            };
            value.AddThemeFontSizeOverride("font_size", 18);
            value.AddThemeColorOverride("font_color", Ivory);
            row.AddChild(value);

            return panel;
        }

        private PanelContainer CreateScorePanel(
            Color accent,
            out Label score,
            out Label multiplier,
            out Label detail)
        {
            PanelContainer panel = CreateFramedPanel();
            MarginContainer margin = AddPadding(panel, 14, 10);

            VBoxContainer column = new VBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center
            };
            column.AddThemeConstantOverride("separation", 1);
            margin.AddChild(column);

            Label heading = CreateHeading("PONTOS", 18, accent);
            column.AddChild(heading);

            score = CreateHeading("0", 54, accent);
            column.AddChild(score);

            multiplier = CreateHeading("MULT × 1,0", 17, accent);
            column.AddChild(multiplier);

            detail = CreateHeading("", 12, Muted);
            detail.CustomMinimumSize = new Vector2(0f, 20f);
            column.AddChild(detail);

            return panel;
        }

        private PanelContainer CreateBoardPanel()
        {
            PanelContainer panel = CreateFramedPanel(
                new Color(0.025f, 0.028f, 0.030f, 0.98f),
                3);

            MarginContainer margin = AddPadding(panel, 16, 16);
            CenterContainer boardCenter = new CenterContainer();
            margin.AddChild(boardCenter);

            _boardView = new BoardView();
            _boardView.CellActivated += OnCellActivated;
            boardCenter.AddChild(_boardView);

            return panel;
        }

        private static PanelContainer CreateFramedPanel(
            Color? background = null,
            int borderWidth = 2)
        {
            PanelContainer panel = new PanelContainer();
            StyleBoxFlat style = new StyleBoxFlat
            {
                BgColor = background ?? PanelDark,
                BorderColor = Bronze,
                BorderWidthLeft = borderWidth,
                BorderWidthTop = borderWidth,
                BorderWidthRight = borderWidth,
                BorderWidthBottom = borderWidth,
                CornerRadiusTopLeft = 5,
                CornerRadiusTopRight = 5,
                CornerRadiusBottomLeft = 5,
                CornerRadiusBottomRight = 5
            };
            panel.AddThemeStyleboxOverride("panel", style);
            return panel;
        }

        private static MarginContainer AddPadding(
            PanelContainer panel,
            int horizontal,
            int vertical)
        {
            MarginContainer margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", horizontal);
            margin.AddThemeConstantOverride("margin_right", horizontal);
            margin.AddThemeConstantOverride("margin_top", vertical);
            margin.AddThemeConstantOverride("margin_bottom", vertical);
            panel.AddChild(margin);
            return margin;
        }

        private static Label CreateHeading(string text, int size, Color color)
        {
            Label label = new Label
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            label.AddThemeFontSizeOverride("font_size", size);
            label.AddThemeColorOverride("font_color", color);
            return label;
        }

        private static ColorRect CreateDivider()
        {
            return new ColorRect
            {
                Color = BronzeSoft,
                CustomMinimumSize = new Vector2(0f, 1f),
                MouseFilter = MouseFilterEnum.Ignore
            };
        }

        private static ProgressBar CreateHealthBar(Color accent)
        {
            ProgressBar bar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = 100,
                ShowPercentage = false,
                CustomMinimumSize = new Vector2(120f, 18f)
            };

            StyleBoxFlat background = new StyleBoxFlat
            {
                BgColor = new Color(0.015f, 0.020f, 0.024f, 1f),
                BorderColor = new Color(0.26f, 0.31f, 0.33f, 1f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8
            };

            StyleBoxFlat fill = new StyleBoxFlat
            {
                BgColor = accent,
                CornerRadiusTopLeft = 7,
                CornerRadiusTopRight = 7,
                CornerRadiusBottomLeft = 7,
                CornerRadiusBottomRight = 7
            };

            bar.AddThemeStyleboxOverride("background", background);
            bar.AddThemeStyleboxOverride("fill", fill);
            return bar;
        }

        private static void StyleButton(Button button)
        {
            StyleBoxFlat normal = new StyleBoxFlat
            {
                BgColor = new Color(0.10f, 0.075f, 0.045f, 0.88f),
                BorderColor = Bronze,
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3
            };

            StyleBoxFlat hover = (StyleBoxFlat)normal.Duplicate();
            hover.BgColor = new Color(0.17f, 0.12f, 0.06f, 0.96f);

            StyleBoxFlat pressed = (StyleBoxFlat)normal.Duplicate();
            pressed.BgColor = new Color(0.08f, 0.06f, 0.04f, 1f);

            button.AddThemeStyleboxOverride("normal", normal);
            button.AddThemeStyleboxOverride("hover", hover);
            button.AddThemeStyleboxOverride("pressed", pressed);
            button.AddThemeColorOverride("font_color", Ivory);
            button.AddThemeColorOverride(
                "font_hover_color",
                new Color(0.94f, 0.84f, 0.66f, 1f));
            button.AddThemeFontSizeOverride("font_size", 15);
        }

        private static void Place(
            Control control,
            float left,
            float top,
            float right,
            float bottom)
        {
            control.AnchorLeft = left;
            control.AnchorTop = top;
            control.AnchorRight = right;
            control.AnchorBottom = bottom;
            control.OffsetLeft = 0f;
            control.OffsetTop = 0f;
            control.OffsetRight = 0f;
            control.OffsetBottom = 0f;
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

        private static BoardState CreateDefaultBoard()
        {
            return CreateRectangularBoard(3, 3);
        }

        private static BoardState CreateRectangularBoard(
            int width,
            int height)
        {
            BoardDefinition definition =
                BoardDefinition.CreateRectangular(width, height);

            BoardCoordinate center = new BoardCoordinate(
                (width - 1) / 2,
                (height - 1) / 2);

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
            {
                throw new InvalidOperationException(
                    "A IA devolveu uma ação fora de LegalActions.");
            }

            _engine.ExecuteAction(selected);
            AfterAuthoritativeAction();
        }

        private void AfterAuthoritativeAction()
        {
            if (_engine.State.Phase == EncounterPhase.RoundResolved)
            {
                RoundResolution resolution =
                    _engine.State.LastRoundResolution;

                _combatAnimator.Present(resolution);
                BeginScoreAnimation(resolution);
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
                Callable.From(RunEnemyTurn).CallDeferred();
            }
        }

        private void OnNextRoundPressed()
        {
            if (_scoreAnimationActive ||
                _engine.State.Phase != EncounterPhase.WaitingForNextRound)
            {
                return;
            }

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

            _roundLabel.Text = $"RODADA {Math.Max(1, state.RoundNumber):00}";

            UpdateHealth(
                _playerHpBar,
                _playerHpText,
                state.PlayerState.CurrentHealth,
                state.PlayerState.MaximumHealth);

            UpdateHealth(
                _enemyHpBar,
                _enemyHpText,
                state.EnemyState.CurrentHealth,
                state.EnemyState.MaximumHealth);

            UpdateTurnDisplay(state);
            UpdateScoreDisplay(state);

            _nextRoundButton.Visible =
                !_scoreAnimationActive &&
                state.Phase == EncounterPhase.WaitingForNextRound;
        }

        private void UpdateTurnDisplay(EncounterState state)
        {
            if (_scoreAnimationActive)
            {
                _turnLabel.Text = "CONTANDO PONTOS";
                _turnLabel.AddThemeColorOverride("font_color", Ivory);
                return;
            }

            if (state.Phase == EncounterPhase.EncounterEnded)
            {
                _turnLabel.Text = "CONFRONTO ENCERRADO";
                _turnLabel.AddThemeColorOverride("font_color", Ivory);
                return;
            }

            if (state.Phase == EncounterPhase.WaitingForNextRound)
            {
                _turnLabel.Text = "RODADA CONCLUÍDA";
                _turnLabel.AddThemeColorOverride("font_color", Ivory);
                return;
            }

            if (state.AcceptsActions && state.CurrentActor == ScoreActor.Player)
            {
                _turnLabel.Text = "SEU TURNO";
                _turnLabel.AddThemeColorOverride("font_color", Cyan);
                return;
            }

            if (state.AcceptsActions && state.CurrentActor == ScoreActor.Enemy)
            {
                _turnLabel.Text = "TURNO DO OPONENTE";
                _turnLabel.AddThemeColorOverride("font_color", Rose);
                return;
            }

            _turnLabel.Text = "RESOLVENDO";
            _turnLabel.AddThemeColorOverride("font_color", Muted);
        }

        private void UpdateScoreDisplay(EncounterState state)
        {
            if (_scoreAnimationActive)
                return;

            int playerScore = 0;
            int enemyScore = 0;
            decimal playerMultiplier = 1m;
            decimal enemyMultiplier = 1m;

            if (state.LastRoundResolution != null)
            {
                playerScore =
                    state.LastRoundResolution.PlayerScore.Breakdown.FinalScore;
                enemyScore =
                    state.LastRoundResolution.EnemyScore.Breakdown.FinalScore;
                playerMultiplier =
                    state.LastRoundResolution.PlayerScore.Breakdown.TotalMultiplier;
                enemyMultiplier =
                    state.LastRoundResolution.EnemyScore.Breakdown.TotalMultiplier;
            }

            _playerScoreLabel.Text =
                playerScore.ToString(CultureInfo.InvariantCulture);
            _enemyScoreLabel.Text =
                enemyScore.ToString(CultureInfo.InvariantCulture);
            _playerMultiplierLabel.Text =
                $"MULT × {FormatMultiplier(playerMultiplier)}";
            _enemyMultiplierLabel.Text =
                $"MULT × {FormatMultiplier(enemyMultiplier)}";

            _playerScoreDetailLabel.Text = "";
            _enemyScoreDetailLabel.Text = "";
        }

        private void BeginScoreAnimation(RoundResolution resolution)
        {
            _animatedResolution = resolution ??
                throw new ArgumentNullException(nameof(resolution));

            _scoreAnimationActive = true;
            _scoreAnimationSide = 0;
            _scoreAnimationStep = 0;
            _scoreStepElapsed = -ScoreSideDelay;

            _playerScoreLabel.Text = "0";
            _enemyScoreLabel.Text = "0";
            _playerMultiplierLabel.Text = "MULT × 1,0";
            _enemyMultiplierLabel.Text = "MULT × 1,0";
            _playerScoreDetailLabel.Text = "CONTANDO...";
            _enemyScoreDetailLabel.Text = "";

            _nextRoundButton.Visible = false;
            SetProcess(true);
        }

        private void AdvanceScoreAnimationSide()
        {
            if (_scoreAnimationSide == 0)
            {
                _playerScoreDetailLabel.Text = "";
                _enemyScoreDetailLabel.Text = "CONTANDO...";

                _scoreAnimationSide = 1;
                _scoreAnimationStep = 0;
                _scoreStepElapsed = -ScoreSideDelay;
                return;
            }

            FinishScoreAnimation();
        }

        private void FinishScoreAnimation()
        {
            if (_animatedResolution != null)
            {
                _playerScoreLabel.Text =
                    _animatedResolution.PlayerScore.Breakdown.FinalScore
                        .ToString(CultureInfo.InvariantCulture);

                _enemyScoreLabel.Text =
                    _animatedResolution.EnemyScore.Breakdown.FinalScore
                        .ToString(CultureInfo.InvariantCulture);

                _playerMultiplierLabel.Text =
                    $"MULT × {FormatMultiplier(_animatedResolution.PlayerScore.Breakdown.TotalMultiplier)}";

                _enemyMultiplierLabel.Text =
                    $"MULT × {FormatMultiplier(_animatedResolution.EnemyScore.Breakdown.TotalMultiplier)}";
            }

            _playerScoreDetailLabel.Text = "";
            _enemyScoreDetailLabel.Text = "";

            _scoreAnimationActive = false;
            _animatedResolution = null;
            SetProcess(false);

            RefreshPresentation();
        }

        private static double GetScoreStepDuration(int stepCount)
        {
            if (stepCount <= 6)
                return 0.42d;

            if (stepCount <= 12)
                return 0.28d;

            return 0.16d;
        }

        private static decimal CalculateVisibleMultiplier(
            ScoreBreakdown breakdown,
            int completedStepCount)
        {
            decimal additiveFactor = 1m;
            decimal independentProduct = 1m;
            decimal victoryProduct = 1m;

            int count = Math.Min(
                completedStepCount,
                breakdown.Steps.Count);

            for (int i = 0; i < count; i++)
            {
                ScoreStep step = breakdown.Steps[i];

                switch (step.Contribution.Phase)
                {
                    case ScorePhase.AdditiveMultiplier:
                        additiveFactor = step.EffectiveFactor;
                        break;

                    case ScorePhase.IndependentMultiplier:
                        independentProduct *= step.EffectiveFactor;
                        break;

                    case ScorePhase.VictoryMultiplier:
                        victoryProduct *= step.EffectiveFactor;
                        break;
                }
            }

            return additiveFactor *
                   independentProduct *
                   victoryProduct;
        }

        private static string BuildScoreStepText(ScoreStep step)
        {
            ScoreContribution contribution = step.Contribution;

            switch (contribution.Operation)
            {
                case ScoreOperation.AddPoints:
                    return $"{contribution.DisplayText}  {FormatSigned(contribution.Amount)}";

                case ScoreOperation.AddToMultiplier:
                    return $"{contribution.DisplayText}  → ×{FormatMultiplier(step.EffectiveFactor)}";

                case ScoreOperation.Multiply:
                    return $"{contribution.DisplayText}  ×{FormatMultiplier(contribution.Amount)}";

                default:
                    return contribution.DisplayText;
            }
        }

        private static string FormatSigned(decimal value)
        {
            string number = Math.Abs(value)
                .ToString("0.#", CultureInfo.InvariantCulture)
                .Replace('.', ',');

            if (value > 0m)
                return $"+{number}";

            if (value < 0m)
                return $"-{number}";

            return "0";
        }

        private static string FormatAnimatedScore(double value)
        {
            double rounded = Math.Round(
                Math.Max(0d, value),
                1,
                MidpointRounding.AwayFromZero);

            return rounded
                .ToString("0.#", CultureInfo.InvariantCulture)
                .Replace('.', ',');
        }

        private static void UpdateHealth(
            ProgressBar bar,
            Label text,
            int current,
            int maximum)
        {
            bar.MaxValue = maximum;
            bar.Value = current;
            text.Text = $"{current} / {maximum}";
        }

        private static string FormatMultiplier(decimal value)
        {
            return value
                .ToString("0.0#", CultureInfo.InvariantCulture)
                .Replace('.', ',');
        }
    }
}
