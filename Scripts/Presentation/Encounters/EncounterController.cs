using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using TicTacToeRoguelike.Application.AI;
using TicTacToeRoguelike.Content.Runes;
using TicTacToeRoguelike.Application.Actions;
using TicTacToeRoguelike.Application.Encounters;
using TicTacToeRoguelike.Application.Effects;
using TicTacToeRoguelike.Domain.Actions;
using TicTacToeRoguelike.Domain.Boards;
using TicTacToeRoguelike.Domain.Combat;
using TicTacToeRoguelike.Domain.Moves;
using TicTacToeRoguelike.Domain.Reactions;
using TicTacToeRoguelike.Domain.Runes;
using TicTacToeRoguelike.Domain.Scoring;
using TicTacToeRoguelike.Domain.Sequences;
using TicTacToeRoguelike.Presentation.Arena;
using TicTacToeRoguelike.Presentation.Board;
using TicTacToeRoguelike.Presentation.Combat;
using TicTacToeRoguelike.Presentation.Runes;

namespace TicTacToeRoguelike.Presentation.Encounters
{
    /// <summary>
    /// Composition root do encontro e da arena mística.
    /// Inclui um menu de teste para variar o tamanho do tabuleiro e a sequência
    /// necessária para vitória sem alterar as regras autoritativas durante a rodada.
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

        private const double ScoreSideDelay = 0.22;
        private const double ScoreSequenceGap = 0.10;
        private const double EnemyTurnVisualDelay = 0.36;
        private const double ClashCancellationDuration = 0.72;
        private const double DamageTelegraphDuration = 0.52;
        private const double HealthReductionDuration = 0.62;

        private enum RoundAnimationPhase
        {
            None = 0,
            ScoreCounting = 1,
            ClashCancellation = 2,
            DamageTelegraph = 3,
            HealthReduction = 4
        }

        private readonly SequenceEvaluator _sequenceEvaluator =
            new SequenceEvaluator();

        private EncounterEngine _engine;
        private ActionCatalog _catalog;
        private BasicTicTacToePolicy _enemyPolicy;
        private BoardView _boardView;
        private CombatReportAnimator _combatAnimator;
        private RuneInventoryView _playerRuneView;
        private RuneInventoryView _enemyRuneView;

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
        private Label _playerFloatingScoreLabel;
        private Label _enemyFloatingScoreLabel;
        private Label _reactionConditionLabel;
        private Label _reactionPlayerCountLabel;
        private Label _reactionEnemyCountLabel;
        private Label _reactionNeedLabel;
        private Label _playerDamageLabel;
        private Label _enemyDamageLabel;

        private ProgressBar _playerHpBar;
        private ProgressBar _enemyHpBar;
        private Control _playerHpPanel;
        private Control _enemyHpPanel;
        private Button _nextRoundButton;

        private SpinBox _boardSizeSpin;
        private SpinBox _victoryLengthSpin;

        private Tween _playerFloatingTween;
        private Tween _enemyFloatingTween;
        private Tween _playerMultiplierTween;
        private Tween _enemyMultiplierTween;

        private int _configuredBoardSize = 3;
        private int _configuredVictoryLength = 3;
        private int _encounterGeneration;
        private bool _enemyTurnScheduled;

        private HashSet<string> _lastPlayerWinningSequenceIds =
            new HashSet<string>();
        private HashSet<string> _lastEnemyWinningSequenceIds =
            new HashSet<string>();

        private bool _scoreAnimationActive;
        private bool _roundEraseActive;
        private bool _scoreStepPrepared;
        private bool _showClashRemainder;
        private RoundResolution _animatedResolution;
        private RoundAnimationPhase _roundAnimationPhase;
        private int _scoreAnimationSide;
        private int _scoreAnimationStep;
        private double _scoreStepElapsed;
        private double _roundAnimationElapsed;
        private Control _damageShakeTarget;
        private Vector2 _damageShakeBasePosition;

        public override void _Ready()
        {
            BuildSetupMenu();
            SetProcess(false);
        }

        public override void _Process(double delta)
        {
            if (!_scoreAnimationActive ||
                _animatedResolution == null)
            {
                SetProcess(false);
                return;
            }

            switch (_roundAnimationPhase)
            {
                case RoundAnimationPhase.ScoreCounting:
                    ProcessScoreCounting(delta);
                    break;

                case RoundAnimationPhase.ClashCancellation:
                    ProcessClashCancellation(delta);
                    break;

                case RoundAnimationPhase.DamageTelegraph:
                    ProcessDamageTelegraph(delta);
                    break;

                case RoundAnimationPhase.HealthReduction:
                    ProcessHealthReduction(delta);
                    break;

                default:
                    SetProcess(false);
                    break;
            }
        }

        private void ProcessScoreCounting(double delta)
        {
            if (_scoreStepElapsed < 0d)
            {
                _scoreStepElapsed += delta;
                return;
            }

            ScorePipelineResult result =
                _scoreAnimationSide == 0
                    ? _animatedResolution.PlayerScore
                    : _animatedResolution.EnemyScore;

            Label scoreLabel =
                _scoreAnimationSide == 0
                    ? _playerScoreLabel
                    : _enemyScoreLabel;

            Label multiplierLabel =
                _scoreAnimationSide == 0
                    ? _playerMultiplierLabel
                    : _enemyMultiplierLabel;

            Label detailLabel =
                _scoreAnimationSide == 0
                    ? _playerScoreDetailLabel
                    : _enemyScoreDetailLabel;

            IReadOnlyList<ScoreStep> steps =
                result.Breakdown.Steps;

            if (_scoreAnimationStep >= steps.Count)
            {
                _boardView.HideScoringSequence();

                scoreLabel.Text =
                    result.Breakdown.FinalScore.ToString(
                        CultureInfo.InvariantCulture);

                multiplierLabel.Text =
                    $"MULT × {FormatMultiplier(result.Breakdown.TotalMultiplier)}";

                detailLabel.Text =
                    steps.Count == 0
                        ? "SEM PONTOS"
                        : "TOTAL";

                AdvanceScoreAnimationSide();
                return;
            }

            ScoreStep step =
                steps[_scoreAnimationStep];

            if (!_scoreStepPrepared)
            {
                PrepareScoreStep(
                    result,
                    step);

                _scoreStepPrepared = true;
            }

            double duration =
                GetScoreStepDuration(steps.Count);

            _scoreStepElapsed += delta;

            double progress =
                Math.Clamp(
                    _scoreStepElapsed / duration,
                    0d,
                    1d);

            double eased =
                1d - Math.Pow(1d - progress, 3d);

            double before =
                (double)step.ScoreBefore;
            double after =
                (double)step.ScoreAfter;

            double visibleScore =
                before +
                ((after - before) * eased);

            scoreLabel.Text =
                FormatAnimatedScore(visibleScore);

            detailLabel.Text =
                BuildScoreStepText(step);

            if (progress < 1d)
                return;

            _boardView.HideScoringSequence();

            decimal previousMultiplier =
                CalculateVisibleMultiplier(
                    result.Breakdown,
                    _scoreAnimationStep);

            _scoreAnimationStep++;
            _scoreStepPrepared = false;
            _scoreStepElapsed =
                -ScoreSequenceGap;

            decimal visibleMultiplier =
                CalculateVisibleMultiplier(
                    result.Breakdown,
                    _scoreAnimationStep);

            multiplierLabel.Text =
                $"MULT × {FormatMultiplier(visibleMultiplier)}";

            if (visibleMultiplier != previousMultiplier)
            {
                BounceMultiplier(
                    multiplierLabel,
                    result.Participant);
            }
        }

        private void ProcessClashCancellation(
            double delta)
        {
            _roundAnimationElapsed += delta;

            double progress =
                Math.Clamp(
                    _roundAnimationElapsed /
                    ClashCancellationDuration,
                    0d,
                    1d);

            double eased =
                progress < 0.5d
                    ? 2d * progress * progress
                    : 1d -
                      Math.Pow(-2d * progress + 2d, 2d) /
                      2d;

            ClashReport clash =
                _animatedResolution.Clash;

            int cancelled =
                (int)Math.Round(
                    clash.CancelledScorePerSide *
                    eased,
                    MidpointRounding.AwayFromZero);

            int playerVisible =
                Math.Max(
                    clash.PlayerRemainingScore,
                    clash.PlayerScore - cancelled);

            int enemyVisible =
                Math.Max(
                    clash.EnemyRemainingScore,
                    clash.EnemyScore - cancelled);

            _playerScoreLabel.Text =
                playerVisible.ToString(
                    CultureInfo.InvariantCulture);

            _enemyScoreLabel.Text =
                enemyVisible.ToString(
                    CultureInfo.InvariantCulture);

            if (progress < 1d)
                return;

            _playerScoreLabel.Text =
                clash.PlayerRemainingScore.ToString(
                    CultureInfo.InvariantCulture);

            _enemyScoreLabel.Text =
                clash.EnemyRemainingScore.ToString(
                    CultureInfo.InvariantCulture);

            BeginDamageTelegraph();
        }

        private void ProcessDamageTelegraph(
            double delta)
        {
            DamageReport damage =
                _animatedResolution.Damage;

            if (!damage.HasTarget ||
                damage.AppliedHealthDamage <= 0)
            {
                FinishScoreAnimation();
                return;
            }

            _roundAnimationElapsed += delta;

            double progress =
                Math.Clamp(
                    _roundAnimationElapsed /
                    DamageTelegraphDuration,
                    0d,
                    1d);

            float decay =
                (float)(1d - progress);

            float shake =
                MathF.Sin(
                    (float)_roundAnimationElapsed *
                    58f) *
                9f *
                decay;

            if (_damageShakeTarget != null)
            {
                _damageShakeTarget.Position =
                    _damageShakeBasePosition +
                    new Vector2(shake, 0f);
            }

            Label damageLabel =
                damage.TargetActor == ScoreActor.Player
                    ? _playerDamageLabel
                    : _enemyDamageLabel;

            if (damageLabel != null)
            {
                float scale =
                    0.72f +
                    (float)(
                        1d -
                        Math.Pow(1d - progress, 3d)) *
                    0.42f;

                damageLabel.Scale =
                    Vector2.One * scale;

                damageLabel.Modulate =
                    new Color(
                        1f,
                        1f,
                        1f,
                        progress > 0.78d
                            ? (float)(
                                1d -
                                ((progress - 0.78d) / 0.22d))
                            : 1f);
            }

            if (progress < 1d)
                return;

            if (_damageShakeTarget != null)
            {
                _damageShakeTarget.Position =
                    _damageShakeBasePosition;
            }

            BeginHealthReduction();
        }

        private void ProcessHealthReduction(
            double delta)
        {
            DamageReport damage =
                _animatedResolution.Damage;

            if (!damage.HasTarget)
            {
                FinishScoreAnimation();
                return;
            }

            _roundAnimationElapsed += delta;

            double progress =
                Math.Clamp(
                    _roundAnimationElapsed /
                    HealthReductionDuration,
                    0d,
                    1d);

            double eased =
                1d - Math.Pow(1d - progress, 3d);

            CombatantSnapshot before =
                damage.TargetBefore;
            CombatantSnapshot after =
                damage.TargetAfter;

            int visibleHealth =
                (int)Math.Round(
                    before.CurrentHealth +
                    (after.CurrentHealth -
                     before.CurrentHealth) *
                    eased,
                    MidpointRounding.AwayFromZero);

            ProgressBar bar =
                damage.TargetActor == ScoreActor.Player
                    ? _playerHpBar
                    : _enemyHpBar;

            Label healthText =
                damage.TargetActor == ScoreActor.Player
                    ? _playerHpText
                    : _enemyHpText;

            bar.MaxValue =
                before.MaximumHealth;
            bar.Value =
                visibleHealth;

            healthText.Text =
                $"{visibleHealth} / {before.MaximumHealth}";

            if (progress < 1d)
                return;

            bar.Value =
                after.CurrentHealth;

            healthText.Text =
                $"{after.CurrentHealth} / {after.MaximumHealth}";

            FinishScoreAnimation();
        }

        private void BuildSetupMenu()
        {
            MysticArenaBackdrop background =
                new MysticArenaBackdrop();
            background.SetAnchorsAndOffsetsPreset(
                LayoutPreset.FullRect);
            AddChild(background);

            CenterContainer center = new CenterContainer();
            center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(center);

            PanelContainer panel = CreateFramedPanel(
                new Color(0.018f, 0.022f, 0.026f, 0.97f),
                2);
            panel.CustomMinimumSize = new Vector2(430f, 360f);
            center.AddChild(panel);

            MarginContainer margin = AddPadding(panel, 34, 26);

            VBoxContainer column = new VBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center
            };
            column.AddThemeConstantOverride("separation", 13);
            margin.AddChild(column);

            Label title =
                CreateHeading("TESTE DA ARENA", 30, Ivory);
            column.AddChild(title);

            Label hint = CreateHeading(
                "Escolha as regras antes de iniciar",
                15,
                Muted);
            column.AddChild(hint);
            column.AddChild(CreateDivider());

            Label boardLabel = new Label
            {
                Text = "Tamanho do tabuleiro"
            };
            boardLabel.AddThemeFontSizeOverride("font_size", 17);
            boardLabel.AddThemeColorOverride("font_color", Ivory);
            column.AddChild(boardLabel);

            _boardSizeSpin = new SpinBox
            {
                MinValue = 3,
                MaxValue = 10,
                Step = 1,
                Value = 3,
                AllowGreater = false,
                AllowLesser = false,
                CustomMinimumSize = new Vector2(250f, 42f)
            };
            _boardSizeSpin.Suffix = " × ";
            _boardSizeSpin.ValueChanged += OnBoardSizeChanged;
            column.AddChild(_boardSizeSpin);

            Label victoryLabel = new Label
            {
                Text = "Sequência necessária para vitória"
            };
            victoryLabel.AddThemeFontSizeOverride("font_size", 17);
            victoryLabel.AddThemeColorOverride("font_color", Ivory);
            column.AddChild(victoryLabel);

            _victoryLengthSpin = new SpinBox
            {
                MinValue = 2,
                MaxValue = 3,
                Step = 1,
                Value = 3,
                AllowGreater = false,
                AllowLesser = false,
                CustomMinimumSize = new Vector2(250f, 42f)
            };
            _victoryLengthSpin.Suffix = " em linha";
            column.AddChild(_victoryLengthSpin);

            Label note = CreateHeading(
                "Pontuam sequências de 2 até o tamanho da vitória.",
                13,
                Muted);
            column.AddChild(note);

            Button start = new Button
            {
                Text = "INICIAR PARTIDA",
                FocusMode = FocusModeEnum.None,
                CustomMinimumSize = new Vector2(250f, 48f)
            };
            StyleButton(start);
            start.Pressed += OnStartConfiguredMatch;
            column.AddChild(start);
        }

        private void OnBoardSizeChanged(double value)
        {
            int size = (int)Math.Round(value);
            _victoryLengthSpin.MaxValue = size;

            if (_victoryLengthSpin.Value > size)
                _victoryLengthSpin.Value = size;
        }

        private void OnStartConfiguredMatch()
        {
            _configuredBoardSize =
                (int)Math.Round(_boardSizeSpin.Value);

            _configuredVictoryLength =
                (int)Math.Round(_victoryLengthSpin.Value);

            ClearCurrentScreen();
            ComposeEncounter();
            BuildInterface();
            StartFirstRound();
        }

        private void ClearCurrentScreen()
        {
            foreach (Node child in GetChildren())
            {
                RemoveChild(child);
                child.QueueFree();
            }
        }

        private void BuildInterface()
        {
            MysticArenaBackdrop background =
                new MysticArenaBackdrop();
            background.SetAnchorsAndOffsetsPreset(
                LayoutPreset.FullRect);
            AddChild(background);

            Control layout = new Control();
            layout.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            layout.MouseFilter = MouseFilterEnum.Pass;
            AddChild(layout);

            PanelContainer opponentRunes =
                CreateRunePanel(
                    "RUNAS DO OPONENTE",
                    _engine.State.EnemyRunes,
                    out _enemyRuneView);
            Place(opponentRunes, 0.055f, 0.050f, 0.340f, 0.205f);
            layout.AddChild(opponentRunes);

            PanelContainer reactionPanel =
                CreateReactionPanel();
            Place(reactionPanel, 0.055f, 0.255f, 0.315f, 0.485f);
            layout.AddChild(reactionPanel);

            PanelContainer roundPanel = CreateRoundPanel();
            Place(roundPanel, 0.410f, 0.035f, 0.590f, 0.165f);
            layout.AddChild(roundPanel);

            PanelContainer opponentHp = CreateHealthPanel(
                "HP OPONENTE",
                Rose,
                out _enemyHpBar,
                out _enemyHpText);
            _enemyHpPanel = opponentHp;
            Place(opponentHp, 0.680f, 0.050f, 0.950f, 0.170f);
            layout.AddChild(opponentHp);

            _enemyDamageLabel =
                CreateDamageLabel();
            Place(
                _enemyDamageLabel,
                0.805f,
                0.096f,
                0.925f,
                0.154f);
            layout.AddChild(_enemyDamageLabel);

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

            _playerFloatingScoreLabel =
                CreateFloatingScoreLabel(Cyan);
            Place(
                _playerFloatingScoreLabel,
                0.770f,
                0.235f,
                0.895f,
                0.305f);
            layout.AddChild(_playerFloatingScoreLabel);

            PanelContainer enemyScore = CreateScorePanel(
                Rose,
                out _enemyScoreLabel,
                out _enemyMultiplierLabel,
                out _enemyScoreDetailLabel);
            Place(enemyScore, 0.750f, 0.465f, 0.915f, 0.670f);
            layout.AddChild(enemyScore);

            _enemyFloatingScoreLabel =
                CreateFloatingScoreLabel(Rose);
            Place(
                _enemyFloatingScoreLabel,
                0.770f,
                0.475f,
                0.895f,
                0.545f);
            layout.AddChild(_enemyFloatingScoreLabel);

            PanelContainer playerRunes =
                CreateRunePanel(
                    "RUNAS DO JOGADOR",
                    _engine.State.PlayerRunes,
                    out _playerRuneView);
            Place(playerRunes, 0.055f, 0.745f, 0.430f, 0.930f);
            layout.AddChild(playerRunes);

            PanelContainer playerHp = CreateHealthPanel(
                "HP JOGADOR",
                Cyan,
                out _playerHpBar,
                out _playerHpText);
            _playerHpPanel = playerHp;
            Place(playerHp, 0.650f, 0.775f, 0.950f, 0.910f);
            layout.AddChild(playerHp);

            _playerDamageLabel =
                CreateDamageLabel();
            Place(
                _playerDamageLabel,
                0.800f,
                0.820f,
                0.925f,
                0.878f);
            layout.AddChild(_playerDamageLabel);

            _combatAnimator = new CombatReportAnimator();
            AddChild(_combatAnimator);
        }

        private PanelContainer CreateRunePanel(
            string title,
            RuneInventoryState inventory,
            out RuneInventoryView inventoryView)
        {
            PanelContainer panel = CreateFramedPanel();
            MarginContainer margin = AddPadding(panel, 18, 12);

            VBoxContainer column = new VBoxContainer();
            column.AddThemeConstantOverride("separation", 8);
            margin.AddChild(column);

            Label heading = CreateHeading(title, 20, Ivory);
            column.AddChild(heading);
            column.AddChild(CreateDivider());

            inventoryView =
                new RuneInventoryView(inventory)
                {
                    SizeFlagsHorizontal =
                        SizeFlags.ExpandFill,
                    SizeFlagsVertical =
                        SizeFlags.ExpandFill
                };

            column.AddChild(inventoryView);

            return panel;
        }

        private PanelContainer CreateReactionPanel()
        {
            PanelContainer panel = CreateFramedPanel();
            MarginContainer margin = AddPadding(panel, 16, 12);

            VBoxContainer column = new VBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center
            };
            column.AddThemeConstantOverride("separation", 5);
            margin.AddChild(column);

            Label heading =
                CreateHeading("CONDIÇÃO DE VITÓRIA", 17, Ivory);
            column.AddChild(heading);

            _reactionConditionLabel =
                CreateHeading("3 EM LINHA", 14, Muted);
            column.AddChild(_reactionConditionLabel);
            column.AddChild(CreateDivider());

            HBoxContainer counts = new HBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center
            };
            counts.AddThemeConstantOverride("separation", 18);
            column.AddChild(counts);

            _reactionPlayerCountLabel =
                CreateHeading("VOCÊ  0", 22, Cyan);
            _reactionPlayerCountLabel.SizeFlagsHorizontal =
                SizeFlags.ExpandFill;
            counts.AddChild(_reactionPlayerCountLabel);

            Label versus =
                CreateHeading("×", 18, Muted);
            counts.AddChild(versus);

            _reactionEnemyCountLabel =
                CreateHeading("0  OPONENTE", 22, Rose);
            _reactionEnemyCountLabel.SizeFlagsHorizontal =
                SizeFlags.ExpandFill;
            counts.AddChild(_reactionEnemyCountLabel);

            _reactionNeedLabel =
                CreateHeading("SEM REAÇÃO", 13, Muted);
            _reactionNeedLabel.AutowrapMode =
                TextServer.AutowrapMode.WordSmart;
            _reactionNeedLabel.CustomMinimumSize =
                new Vector2(0f, 34f);
            column.AddChild(_reactionNeedLabel);

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

            _roundLabel =
                CreateHeading("RODADA 01", 28, Ivory);
            column.AddChild(_roundLabel);

            _turnLabel =
                CreateHeading("SEU TURNO", 20, Cyan);
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
                HorizontalAlignment =
                    HorizontalAlignment.Left
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
                HorizontalAlignment =
                    HorizontalAlignment.Right,
                VerticalAlignment =
                    VerticalAlignment.Center,
                CustomMinimumSize =
                    new Vector2(96f, 28f)
            };
            value.AddThemeFontSizeOverride("font_size", 18);
            value.AddThemeColorOverride("font_color", Ivory);
            row.AddChild(value);

            return panel;
        }

        private static Label CreateDamageLabel()
        {
            Label label =
                CreateHeading("", 32, Rose);

            label.Visible = false;
            label.MouseFilter =
                MouseFilterEnum.Ignore;
            label.ZIndex = 30;

            return label;
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

            Label heading =
                CreateHeading("PONTOS", 18, accent);
            column.AddChild(heading);

            score = CreateHeading("0", 54, accent);
            column.AddChild(score);

            multiplier =
                CreateHeading("MULT × 1,0", 17, accent);
            column.AddChild(multiplier);

            detail = CreateHeading("", 12, Muted);
            detail.CustomMinimumSize = new Vector2(0f, 20f);
            column.AddChild(detail);

            return panel;
        }

        private static Label CreateFloatingScoreLabel(
            Color accent)
        {
            Label label = CreateHeading("", 34, accent);
            label.Visible = false;
            label.MouseFilter = MouseFilterEnum.Ignore;
            label.ZIndex = 20;
            return label;
        }

        private PanelContainer CreateBoardPanel()
        {
            PanelContainer panel = CreateFramedPanel(
                new Color(0.025f, 0.028f, 0.030f, 0.98f),
                3);

            MarginContainer margin =
                AddPadding(panel, 16, 16);

            CenterContainer boardCenter =
                new CenterContainer();
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
            margin.AddThemeConstantOverride(
                "margin_left",
                horizontal);
            margin.AddThemeConstantOverride(
                "margin_right",
                horizontal);
            margin.AddThemeConstantOverride(
                "margin_top",
                vertical);
            margin.AddThemeConstantOverride(
                "margin_bottom",
                vertical);
            panel.AddChild(margin);
            return margin;
        }

        private static Label CreateHeading(
            string text,
            int size,
            Color color)
        {
            Label label = new Label
            {
                Text = text,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                VerticalAlignment =
                    VerticalAlignment.Center
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
                CustomMinimumSize =
                    new Vector2(0f, 1f),
                MouseFilter =
                    MouseFilterEnum.Ignore
            };
        }

        private static ProgressBar CreateHealthBar(
            Color accent)
        {
            ProgressBar bar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = 100,
                ShowPercentage = false,
                CustomMinimumSize =
                    new Vector2(120f, 18f)
            };

            StyleBoxFlat background = new StyleBoxFlat
            {
                BgColor =
                    new Color(0.015f, 0.020f, 0.024f, 1f),
                BorderColor =
                    new Color(0.26f, 0.31f, 0.33f, 1f),
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

            bar.AddThemeStyleboxOverride(
                "background",
                background);
            bar.AddThemeStyleboxOverride(
                "fill",
                fill);

            return bar;
        }

        private static void StyleButton(Button button)
        {
            StyleBoxFlat normal = new StyleBoxFlat
            {
                BgColor =
                    new Color(0.10f, 0.075f, 0.045f, 0.88f),
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

            StyleBoxFlat hover =
                (StyleBoxFlat)normal.Duplicate();
            hover.BgColor =
                new Color(0.17f, 0.12f, 0.06f, 0.96f);

            StyleBoxFlat pressed =
                (StyleBoxFlat)normal.Duplicate();
            pressed.BgColor =
                new Color(0.08f, 0.06f, 0.04f, 1f);

            button.AddThemeStyleboxOverride("normal", normal);
            button.AddThemeStyleboxOverride("hover", hover);
            button.AddThemeStyleboxOverride("pressed", pressed);
            button.AddThemeColorOverride(
                "font_color",
                Ivory);
            button.AddThemeColorOverride(
                "font_hover_color",
                new Color(0.94f, 0.84f, 0.66f, 1f));
            button.AddThemeFontSizeOverride(
                "font_size",
                15);
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
            MoveValidator moveValidator =
                new MoveValidator();

            MoveService moveService =
                new MoveService(moveValidator);

            _catalog =
                ActionCatalog.CreateDefault(
                    moveValidator,
                    moveService);

            ActionExecutor executor =
                ActionExecutor.CreateWithCatalog(_catalog);

            ActionAvailabilityService availability =
                new ActionAvailabilityService(
                    moveValidator,
                    Array.Empty<IActionAvailabilityProvider>());

            CombatantState player =
                new CombatantState(
                    "player",
                    ScoreActor.Player,
                    100);

            CombatantState enemy =
                new CombatantState(
                    "enemy:default",
                    ScoreActor.Enemy,
                    100);

            EncounterRules rules =
                new EncounterRules(
                    requiredSequenceLength:
                        _configuredVictoryLength,
                    minimumScoringSequenceLength: 2,
                    maximumScoringSequenceLength:
                        _configuredVictoryLength,
                    victoryMultiplier: 1.5m);

            RuneInventoryState playerRunes =
                StarterRuneCatalog.CreatePlayerInventory();

            RuneInventoryState enemyRunes =
                StarterRuneCatalog.CreateEnemyInventory();

            _engine = new EncounterEngine(
                player,
                enemy,
                rules,
                playerRunes,
                enemyRunes,
                DefaultEffectEngineFactory.Create(),
                executor,
                availability,
                new ReactionStateFactory(),
                new ReactionRule(),
                new ScorePipeline(),
                new ClashResolver(),
                new DamageResolver(),
                new AlternatingEncounterTurnScheduler());

            int searchDepth =
                _configuredBoardSize <= 4 ? 10 : 7;

            _enemyPolicy = new BasicTicTacToePolicy(
                maximumSearchDepth: searchDepth,
                maximumVisitedNodes: 100_000);
        }

        private void StartFirstRound()
        {
            _encounterGeneration++;
            _roundEraseActive = false;
            _showClashRemainder = false;
            ResetReactionVisualTracking();

            _engine.StartEncounter(
                CreateConfiguredBoard(),
                EncounterSide.Player);

            RefreshPresentation();
            RefreshReactionVisuals(false);
            ScheduleEnemyTurnIfNeeded();
        }

        private BoardState CreateConfiguredBoard()
        {
            BoardDefinition definition =
                BoardDefinition.CreateRectangular(
                    _configuredBoardSize,
                    _configuredBoardSize);

            // A casa dourada só existe quando há uma casa central única.
            // Em tabuleiros pares existem quatro casas centrais possíveis, então
            // nenhuma delas recebe o modificador Golden.
            if (_configuredBoardSize % 2 == 0)
            {
                return new BoardState(definition);
            }

            int centerIndex =
                _configuredBoardSize / 2;

            BoardCoordinate center =
                new BoardCoordinate(
                    centerIndex,
                    centerIndex);

            CellState goldenCenter =
                new CellState(
                    center,
                    CellMark.None,
                    new[] { CellModifierId.Golden });

            return new BoardState(
                definition,
                new[] { goldenCenter });
        }

        private void OnCellActivated(
            BoardCoordinate coordinate)
        {
            if (!_engine.State.AcceptsActions ||
                _engine.State.CurrentActor !=
                ScoreActor.Player)
            {
                return;
            }

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
                _engine.State.CurrentActor !=
                ScoreActor.Enemy)
            {
                return;
            }

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

            if (!_enemyPolicy.TryChooseAction(
                    context,
                    out GameAction selected))
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
            RoundResolution resolvedRound = null;

            if (_engine.State.Phase ==
                EncounterPhase.RoundResolved)
            {
                resolvedRound =
                    _engine.State.LastRoundResolution;

                _combatAnimator.Present(resolvedRound);
                _engine.AcknowledgeRoundResolution();
            }

            // Atualiza imediatamente o tabuleiro. A IA só começa depois de um
            // pequeno intervalo, garantindo ao Godot ao menos alguns frames para
            // desenhar o símbolo e a mudança da reação.
            RefreshPresentation();
            RefreshReactionVisuals(true);

            if (resolvedRound != null)
            {
                BeginScoreAnimation(resolvedRound);
                UpdateTurnDisplay(_engine.State);
            }

            ScheduleEnemyTurnIfNeeded();
        }

        private void ScheduleEnemyTurnIfNeeded()
        {
            if (_enemyTurnScheduled)
                return;

            if (_engine.State.AcceptsActions &&
                _engine.State.CurrentActor ==
                ScoreActor.Enemy)
            {
                _enemyTurnScheduled = true;
                int scheduledGeneration =
                    _encounterGeneration;

                RunEnemyTurnAfterVisualDelay(
                    scheduledGeneration);
            }
        }

        private async void RunEnemyTurnAfterVisualDelay(
            int scheduledGeneration)
        {
            await ToSignal(
                GetTree().CreateTimer(
                    EnemyTurnVisualDelay),
                SceneTreeTimer.SignalName.Timeout);

            if (scheduledGeneration !=
                _encounterGeneration)
            {
                _enemyTurnScheduled = false;
                return;
            }

            RunEnemyTurn();
        }

        private void OnNextRoundPressed()
        {
            if (_scoreAnimationActive ||
                _roundEraseActive ||
                _engine.State.Phase !=
                EncounterPhase.WaitingForNextRound)
            {
                return;
            }

            _encounterGeneration++;
            _roundEraseActive = false;
            _showClashRemainder = false;
            _nextRoundButton.Visible = false;

            _boardView.ClearReactionHighlights();
            ResetReactionVisualTracking();

            _engine.StartNextRound(
                CreateConfiguredBoard(),
                EncounterSide.Player);

            RefreshPresentation();
            RefreshReactionVisuals(false);
            ScheduleEnemyTurnIfNeeded();
        }

        private void RefreshPresentation()
        {
            EncounterState state = _engine.State;

            bool playerCanClick =
                state.AcceptsActions &&
                state.CurrentActor == ScoreActor.Player;

            if (state.Board != null)
            {
                _boardView.RenderBoard(
                    state.Board,
                    playerCanClick);
            }

            _roundLabel.Text =
                $"RODADA {Math.Max(1, state.RoundNumber):00}";

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
            UpdateReactionDisplay(state);

            _nextRoundButton.Visible =
                !_scoreAnimationActive &&
                !_roundEraseActive &&
                state.Phase ==
                EncounterPhase.WaitingForNextRound;
        }

        private void UpdateTurnDisplay(
            EncounterState state)
        {
            if (_roundEraseActive)
            {
                _turnLabel.Text =
                    "APAGANDO TABULEIRO";
                _turnLabel.AddThemeColorOverride(
                    "font_color",
                    Muted);
                return;
            }

            if (_scoreAnimationActive)
            {
                _turnLabel.Text = "CONTANDO PONTOS";
                _turnLabel.AddThemeColorOverride(
                    "font_color",
                    Ivory);
                return;
            }

            if (state.Phase ==
                EncounterPhase.EncounterEnded)
            {
                _turnLabel.Text = "CONFRONTO ENCERRADO";
                _turnLabel.AddThemeColorOverride(
                    "font_color",
                    Ivory);
                return;
            }

            if (state.Phase ==
                EncounterPhase.WaitingForNextRound)
            {
                _turnLabel.Text = "RODADA CONCLUÍDA";
                _turnLabel.AddThemeColorOverride(
                    "font_color",
                    Ivory);
                return;
            }

            if (state.AcceptsActions &&
                state.CurrentActor == ScoreActor.Player)
            {
                _turnLabel.Text = "SEU TURNO";
                _turnLabel.AddThemeColorOverride(
                    "font_color",
                    Cyan);
                return;
            }

            if (state.AcceptsActions &&
                state.CurrentActor == ScoreActor.Enemy)
            {
                _turnLabel.Text =
                    "TURNO DO OPONENTE";
                _turnLabel.AddThemeColorOverride(
                    "font_color",
                    Rose);
                return;
            }

            _turnLabel.Text = "RESOLVENDO";
            _turnLabel.AddThemeColorOverride(
                "font_color",
                Muted);
        }

        private void UpdateReactionDisplay(
            EncounterState state)
        {
            if (_reactionConditionLabel == null)
                return;

            _reactionConditionLabel.Text =
                $"{_configuredVictoryLength} EM LINHA";

            ReactionState reaction =
                state.ReactionState;

            if (reaction == null)
            {
                _reactionPlayerCountLabel.Text =
                    "VOCÊ  0";
                _reactionEnemyCountLabel.Text =
                    "0  OPONENTE";
                _reactionNeedLabel.Text =
                    "SEM REAÇÃO";
                _reactionNeedLabel.AddThemeColorOverride(
                    "font_color",
                    Muted);
                return;
            }

            int player =
                reaction.PlayerSequenceCount;
            int enemy =
                reaction.EnemySequenceCount;

            _reactionPlayerCountLabel.Text =
                $"VOCÊ  {player}";
            _reactionEnemyCountLabel.Text =
                $"{enemy}  OPONENTE";

            if (player == enemy)
            {
                _reactionNeedLabel.Text =
                    player == 0
                        ? "SEM REAÇÃO"
                        : "EMPATE — REAÇÃO ENCERRADA";
                _reactionNeedLabel.AddThemeColorOverride(
                    "font_color",
                    Muted);
                return;
            }

            int difference =
                Math.Abs(player - enemy);

            bool roundAlreadyEnded =
                state.Phase ==
                    EncounterPhase.WaitingForNextRound ||
                state.Phase ==
                    EncounterPhase.EncounterEnded ||
                _scoreAnimationActive;

            if (roundAlreadyEnded)
            {
                bool playerLeads = player > enemy;
                _reactionNeedLabel.Text =
                    playerLeads
                        ? $"VANTAGEM FINAL: VOCÊ +{difference}"
                        : $"VANTAGEM FINAL: OPONENTE +{difference}";
                _reactionNeedLabel.AddThemeColorOverride(
                    "font_color",
                    playerLeads ? Cyan : Rose);
                return;
            }

            if (player < enemy)
            {
                _reactionNeedLabel.Text =
                    $"VOCÊ PRECISA DE +{difference} " +
                    (difference == 1
                        ? "SEQUÊNCIA PARA EMPATAR"
                        : "SEQUÊNCIAS PARA EMPATAR");

                _reactionNeedLabel.AddThemeColorOverride(
                    "font_color",
                    Cyan);
                return;
            }

            _reactionNeedLabel.Text =
                $"OPONENTE PRECISA DE +{difference} " +
                (difference == 1
                    ? "SEQUÊNCIA PARA EMPATAR"
                    : "SEQUÊNCIAS PARA EMPATAR");

            _reactionNeedLabel.AddThemeColorOverride(
                "font_color",
                Rose);
        }

        private void UpdateScoreDisplay(
            EncounterState state)
        {
            if (_scoreAnimationActive)
                return;

            int playerScore = 0;
            int enemyScore = 0;
            decimal playerMultiplier = 1m;
            decimal enemyMultiplier = 1m;

            if (state.LastRoundResolution != null)
            {
                if (_showClashRemainder)
                {
                    playerScore =
                        state.LastRoundResolution
                            .Clash
                            .PlayerRemainingScore;

                    enemyScore =
                        state.LastRoundResolution
                            .Clash
                            .EnemyRemainingScore;
                }
                else
                {
                    playerScore =
                        state.LastRoundResolution
                            .PlayerScore
                            .Breakdown
                            .FinalScore;

                    enemyScore =
                        state.LastRoundResolution
                            .EnemyScore
                            .Breakdown
                            .FinalScore;
                }

                playerMultiplier =
                    state.LastRoundResolution
                        .PlayerScore
                        .Breakdown
                        .TotalMultiplier;

                enemyMultiplier =
                    state.LastRoundResolution
                        .EnemyScore
                        .Breakdown
                        .TotalMultiplier;
            }

            _playerScoreLabel.Text =
                playerScore.ToString(
                    CultureInfo.InvariantCulture);

            _enemyScoreLabel.Text =
                enemyScore.ToString(
                    CultureInfo.InvariantCulture);

            _playerMultiplierLabel.Text =
                $"MULT × {FormatMultiplier(playerMultiplier)}";

            _enemyMultiplierLabel.Text =
                $"MULT × {FormatMultiplier(enemyMultiplier)}";

            _playerScoreDetailLabel.Text = "";
            _enemyScoreDetailLabel.Text = "";
        }

        private void BeginScoreAnimation(
            RoundResolution resolution)
        {
            _animatedResolution = resolution ??
                throw new ArgumentNullException(
                    nameof(resolution));

            _scoreAnimationActive = true;
            _showClashRemainder = false;
            _roundAnimationPhase =
                RoundAnimationPhase.ScoreCounting;
            _scoreAnimationSide = 0;
            _scoreAnimationStep = 0;
            _scoreStepPrepared = false;
            _scoreStepElapsed = -ScoreSideDelay;
            _roundAnimationElapsed = 0d;

            _boardView.HideScoringSequence();

            _playerScoreLabel.Text = "0";
            _enemyScoreLabel.Text = "0";
            _playerMultiplierLabel.Text = "MULT × 1,0";
            _enemyMultiplierLabel.Text = "MULT × 1,0";
            _playerScoreDetailLabel.Text = "CONTANDO...";
            _enemyScoreDetailLabel.Text = "";

            // O domínio já aplicou o dano, mas a interface segura a fotografia
            // anterior até terminar pontuação -> confronto -> impacto -> vida.
            UpdateHealth(
                _playerHpBar,
                _playerHpText,
                resolution.PlayerBefore.CurrentHealth,
                resolution.PlayerBefore.MaximumHealth);

            UpdateHealth(
                _enemyHpBar,
                _enemyHpText,
                resolution.EnemyBefore.CurrentHealth,
                resolution.EnemyBefore.MaximumHealth);

            HideDamageLabels();

            _nextRoundButton.Visible = false;
            SetProcess(true);
        }

        private void PrepareScoreStep(
            ScorePipelineResult result,
            ScoreStep step)
        {
            _boardView.HideScoringSequence();

            PulseContributionSource(
                step.Contribution);

            SequenceMatch sequence =
                FindSequenceForStep(result, step);

            if (sequence == null)
                return;

            _boardView.ShowScoringSequence(sequence);

            ShowFloatingSequenceValue(
                result.Participant,
                step.Contribution.Amount);
        }

        private void PulseContributionSource(
            ScoreContribution contribution)
        {
            if (contribution == null ||
                string.IsNullOrWhiteSpace(
                    contribution.SourceInstanceId))
            {
                return;
            }

            RuneInventoryView inventoryView = null;

            if (contribution.SourceOwner ==
                ScoreActor.Player)
            {
                inventoryView = _playerRuneView;
            }
            else if (contribution.SourceOwner ==
                     ScoreActor.Enemy)
            {
                inventoryView = _enemyRuneView;
            }

            inventoryView?.PulseSource(
                contribution.SourceInstanceId);
        }

        private static SequenceMatch FindSequenceForStep(
            ScorePipelineResult result,
            ScoreStep step)
        {
            if (step.Contribution.Phase !=
                ScorePhase.BasePoints)
            {
                return null;
            }

            for (int i = 0;
                 i < result.ScoringSequences.Count;
                 i++)
            {
                SequenceMatch sequence =
                    result.ScoringSequences[i];

                if (string.Equals(
                    ScorePipeline.GetSequenceSourceId(sequence),
                    step.Contribution.SourceId,
                    StringComparison.Ordinal))
                {
                    return sequence;
                }
            }

            return null;
        }

        private void ShowFloatingSequenceValue(
            ScoreActor actor,
            decimal amount)
        {
            Label label = actor == ScoreActor.Player
                ? _playerFloatingScoreLabel
                : _enemyFloatingScoreLabel;

            if (label == null)
                return;

            Tween previous = actor == ScoreActor.Player
                ? _playerFloatingTween
                : _enemyFloatingTween;

            previous?.Kill();

            label.Text = FormatSigned(amount);
            label.Visible = true;
            label.Scale = new Vector2(0.72f, 0.72f);
            label.Modulate = Colors.White;

            Tween tween = CreateTween();

            tween.TweenProperty(
                    label,
                    "scale",
                    new Vector2(1.10f, 1.10f),
                    0.12d)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);

            tween.TweenInterval(0.10d);

            tween.TweenProperty(
                label,
                "modulate:a",
                0f,
                0.26d);

            tween.TweenCallback(
                Callable.From(
                    () => label.Visible = false));

            if (actor == ScoreActor.Player)
                _playerFloatingTween = tween;
            else
                _enemyFloatingTween = tween;
        }

        private void AdvanceScoreAnimationSide()
        {
            _boardView.HideScoringSequence();

            if (_scoreAnimationSide == 0)
            {
                _playerScoreDetailLabel.Text = "";
                _enemyScoreDetailLabel.Text = "CONTANDO...";

                _scoreAnimationSide = 1;
                _scoreAnimationStep = 0;
                _scoreStepPrepared = false;
                _scoreStepElapsed =
                    -ScoreSideDelay;
                return;
            }

            BeginClashCancellation();
        }

        private void BeginClashCancellation()
        {
            _boardView.HideScoringSequence();

            _roundAnimationPhase =
                RoundAnimationPhase.ClashCancellation;
            _roundAnimationElapsed = 0d;

            _playerScoreDetailLabel.Text =
                "ANULANDO";
            _enemyScoreDetailLabel.Text =
                "ANULANDO";

            _turnLabel.Text =
                "CONFRONTO DE PONTOS";
            _turnLabel.AddThemeColorOverride(
                "font_color",
                Ivory);

            _playerScoreLabel.Text =
                _animatedResolution
                    .Clash
                    .PlayerScore
                    .ToString(
                        CultureInfo.InvariantCulture);

            _enemyScoreLabel.Text =
                _animatedResolution
                    .Clash
                    .EnemyScore
                    .ToString(
                        CultureInfo.InvariantCulture);
        }

        private void BeginDamageTelegraph()
        {
            _roundAnimationPhase =
                RoundAnimationPhase.DamageTelegraph;
            _roundAnimationElapsed = 0d;

            _playerScoreDetailLabel.Text = "";
            _enemyScoreDetailLabel.Text = "";

            DamageReport damage =
                _animatedResolution.Damage;

            if (!damage.HasTarget ||
                damage.AppliedHealthDamage <= 0)
            {
                FinishScoreAnimation();
                return;
            }

            _turnLabel.Text =
                "IMPACTO";
            _turnLabel.AddThemeColorOverride(
                "font_color",
                Rose);

            Label damageLabel =
                damage.TargetActor == ScoreActor.Player
                    ? _playerDamageLabel
                    : _enemyDamageLabel;

            _damageShakeTarget =
                damage.TargetActor == ScoreActor.Player
                    ? _playerHpPanel
                    : _enemyHpPanel;

            if (_damageShakeTarget != null)
            {
                _damageShakeBasePosition =
                    _damageShakeTarget.Position;
            }

            damageLabel.Text =
                $"-{damage.AppliedHealthDamage}";
            damageLabel.Visible = true;
            damageLabel.Modulate = Colors.White;
            damageLabel.Scale =
                new Vector2(0.72f, 0.72f);
            damageLabel.PivotOffset =
                damageLabel.Size * 0.5f;
        }

        private void BeginHealthReduction()
        {
            _roundAnimationPhase =
                RoundAnimationPhase.HealthReduction;
            _roundAnimationElapsed = 0d;

            _turnLabel.Text =
                "DANO";
            _turnLabel.AddThemeColorOverride(
                "font_color",
                Rose);
        }

        private void BounceMultiplier(
            Label multiplierLabel,
            ScoreActor actor)
        {
            Tween previous =
                actor == ScoreActor.Player
                    ? _playerMultiplierTween
                    : _enemyMultiplierTween;

            previous?.Kill();

            multiplierLabel.PivotOffset =
                multiplierLabel.Size * 0.5f;
            multiplierLabel.Scale =
                Vector2.One;

            Tween tween =
                CreateTween();

            tween.TweenProperty(
                    multiplierLabel,
                    "scale",
                    new Vector2(1.30f, 1.30f),
                    0.11d)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);

            tween.TweenProperty(
                    multiplierLabel,
                    "scale",
                    Vector2.One,
                    0.16d)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);

            if (actor == ScoreActor.Player)
                _playerMultiplierTween = tween;
            else
                _enemyMultiplierTween = tween;
        }

        private void HideDamageLabels()
        {
            if (_playerDamageLabel != null)
            {
                _playerDamageLabel.Visible = false;
                _playerDamageLabel.Scale =
                    Vector2.One;
                _playerDamageLabel.Modulate =
                    Colors.White;
            }

            if (_enemyDamageLabel != null)
            {
                _enemyDamageLabel.Visible = false;
                _enemyDamageLabel.Scale =
                    Vector2.One;
                _enemyDamageLabel.Modulate =
                    Colors.White;
            }
        }

        private void FinishScoreAnimation()
        {
            _boardView.HideScoringSequence();
            _boardView.ClearReactionHighlights();
            ResetReactionVisualTracking();

            if (_damageShakeTarget != null)
            {
                _damageShakeTarget.Position =
                    _damageShakeBasePosition;
            }

            HideDamageLabels();

            if (_animatedResolution != null)
            {
                _playerScoreLabel.Text =
                    _animatedResolution
                        .Clash
                        .PlayerRemainingScore
                        .ToString(
                            CultureInfo.InvariantCulture);

                _enemyScoreLabel.Text =
                    _animatedResolution
                        .Clash
                        .EnemyRemainingScore
                        .ToString(
                            CultureInfo.InvariantCulture);

                _playerMultiplierLabel.Text =
                    $"MULT × {FormatMultiplier(_animatedResolution.PlayerScore.Breakdown.TotalMultiplier)}";

                _enemyMultiplierLabel.Text =
                    $"MULT × {FormatMultiplier(_animatedResolution.EnemyScore.Breakdown.TotalMultiplier)}";

                UpdateHealth(
                    _playerHpBar,
                    _playerHpText,
                    _animatedResolution.PlayerAfter.CurrentHealth,
                    _animatedResolution.PlayerAfter.MaximumHealth);

                UpdateHealth(
                    _enemyHpBar,
                    _enemyHpText,
                    _animatedResolution.EnemyAfter.CurrentHealth,
                    _animatedResolution.EnemyAfter.MaximumHealth);
            }

            _playerScoreDetailLabel.Text = "";
            _enemyScoreDetailLabel.Text = "";

            _scoreAnimationActive = false;
            _scoreStepPrepared = false;
            _showClashRemainder = true;
            _roundAnimationPhase =
                RoundAnimationPhase.None;
            _damageShakeTarget = null;
            _animatedResolution = null;
            SetProcess(false);

            BeginRoundErase();
        }

        private void BeginRoundErase()
        {
            if (_boardView == null)
            {
                RefreshPresentation();
                return;
            }

            _roundEraseActive = true;
            _nextRoundButton.Visible = false;

            _turnLabel.Text =
                "APAGANDO TABULEIRO";
            _turnLabel.AddThemeColorOverride(
                "font_color",
                Muted);

            _boardView.PlayRoundErase(
                OnRoundEraseFinished);
        }

        private void OnRoundEraseFinished()
        {
            _roundEraseActive = false;
            RefreshPresentation();
        }

        private void RefreshReactionVisuals(
            bool animateIfChanged)
        {
            if (_engine?.State?.Board == null ||
                _boardView == null)
            {
                return;
            }

            IReadOnlyList<SequenceMatch> playerSequences =
                _sequenceEvaluator.EvaluateWinningSequences(
                    _engine.State.Board,
                    CellMark.X,
                    _configuredVictoryLength);

            IReadOnlyList<SequenceMatch> enemySequences =
                _sequenceEvaluator.EvaluateWinningSequences(
                    _engine.State.Board,
                    CellMark.O,
                    _configuredVictoryLength);

            HashSet<string> playerIds =
                BuildSequenceIdSet(playerSequences);
            HashSet<string> enemyIds =
                BuildSequenceIdSet(enemySequences);

            bool changed =
                !_lastPlayerWinningSequenceIds.SetEquals(
                    playerIds) ||
                !_lastEnemyWinningSequenceIds.SetEquals(
                    enemyIds);

            _boardView.ShowReactionTransition(
                playerSequences,
                enemySequences,
                animateIfChanged && changed);

            _lastPlayerWinningSequenceIds =
                playerIds;
            _lastEnemyWinningSequenceIds =
                enemyIds;
        }

        private static HashSet<string> BuildSequenceIdSet(
            IReadOnlyList<SequenceMatch> sequences)
        {
            HashSet<string> ids =
                new HashSet<string>();

            for (int i = 0;
                 i < sequences.Count;
                 i++)
            {
                ids.Add(
                    ScorePipeline.GetSequenceSourceId(
                        sequences[i]));
            }

            return ids;
        }

        private void ResetReactionVisualTracking()
        {
            _lastPlayerWinningSequenceIds =
                new HashSet<string>();
            _lastEnemyWinningSequenceIds =
                new HashSet<string>();
        }

        private static double GetScoreStepDuration(
            int stepCount)
        {
            if (stepCount <= 6)
                return 0.52d;

            if (stepCount <= 12)
                return 0.38d;

            if (stepCount <= 24)
                return 0.26d;

            return 0.18d;
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
                        additiveFactor =
                            step.EffectiveFactor;
                        break;

                    case ScorePhase.IndependentMultiplier:
                        independentProduct *=
                            step.EffectiveFactor;
                        break;

                    case ScorePhase.VictoryMultiplier:
                        victoryProduct *=
                            step.EffectiveFactor;
                        break;
                }
            }

            return additiveFactor *
                   independentProduct *
                   victoryProduct;
        }

        private static string BuildScoreStepText(
            ScoreStep step)
        {
            ScoreContribution contribution =
                step.Contribution;

            switch (contribution.Operation)
            {
                case ScoreOperation.AddPoints:
                    return
                        $"{contribution.DisplayText}  {FormatSigned(contribution.Amount)}";

                case ScoreOperation.AddToMultiplier:
                    return
                        $"{contribution.DisplayText}  → ×{FormatMultiplier(step.EffectiveFactor)}";

                case ScoreOperation.Multiply:
                    return
                        $"{contribution.DisplayText}  ×{FormatMultiplier(contribution.Amount)}";

                default:
                    return contribution.DisplayText;
            }
        }

        private static string FormatSigned(
            decimal value)
        {
            string number = Math.Abs(value)
                .ToString(
                    "0.#",
                    CultureInfo.InvariantCulture)
                .Replace('.', ',');

            if (value > 0m)
                return $"+{number}";

            if (value < 0m)
                return $"-{number}";

            return "0";
        }

        private static string FormatAnimatedScore(
            double value)
        {
            double rounded = Math.Round(
                Math.Max(0d, value),
                1,
                MidpointRounding.AwayFromZero);

            return rounded
                .ToString(
                    "0.#",
                    CultureInfo.InvariantCulture)
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

        private static string FormatMultiplier(
            decimal value)
        {
            return value
                .ToString(
                    "0.0#",
                    CultureInfo.InvariantCulture)
                .Replace('.', ',');
        }
    }
}
