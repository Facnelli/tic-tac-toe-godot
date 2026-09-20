using System;
using Godot;

namespace TicTacToeRoguelike.Presentation.Arena
{
    /// <summary>
    /// Fundo procedural provisório da arena. Reproduz a composição mística da
    /// referência sem depender de sprites finais, mantendo o layout iterável.
    /// </summary>
    public sealed partial class MysticArenaBackdrop : Control
    {
        private static readonly Color BaseStone = new Color(0.022f, 0.028f, 0.034f, 1f);
        private static readonly Color StoneLift = new Color(0.052f, 0.060f, 0.066f, 1f);
        private static readonly Color RuneLine = new Color(0.47f, 0.29f, 0.13f, 0.26f);
        private static readonly Color RuneLineSoft = new Color(0.35f, 0.23f, 0.12f, 0.14f);
        private static readonly Color Crack = new Color(0.29f, 0.31f, 0.32f, 0.11f);

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            QueueRedraw();
        }

        public override void _Draw()
        {
            Vector2 size = Size;
            DrawRect(new Rect2(Vector2.Zero, size), BaseStone);

            // Leve variação tonal para evitar aparência de fundo chapado.
            DrawRect(
                new Rect2(size * 0.015f, new Vector2(size.X * 0.97f, size.Y * 0.96f)),
                StoneLift);

            Vector2 center = size * 0.5f;
            float radiusBase = MathF.Min(size.X, size.Y);

            for (int i = 0; i < 6; i++)
            {
                float radius = radiusBase * (0.23f + i * 0.047f);
                DrawArc(center, radius, 0f, MathF.Tau, 160, RuneLineSoft, 1.2f, true);
            }

            for (int i = 0; i < 12; i++)
            {
                float angle = MathF.Tau * i / 12f;
                Vector2 direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                Vector2 start = center + direction * radiusBase * 0.19f;
                Vector2 end = center + direction * radiusBase * 0.48f;
                DrawLine(start, end, RuneLineSoft, 1f, true);
            }

            DrawLine(
                new Vector2(size.X * 0.02f, center.Y),
                new Vector2(size.X * 0.98f, center.Y),
                RuneLine,
                1.1f,
                true);

            DrawLine(
                new Vector2(center.X, size.Y * 0.015f),
                new Vector2(center.X, size.Y * 0.985f),
                RuneLineSoft,
                1f,
                true);

            DrawArc(center, radiusBase * 0.145f, 0f, MathF.Tau, 100, RuneLine, 1.4f, true);
            DrawArc(center, radiusBase * 0.095f, 0f, MathF.Tau, 80, RuneLineSoft, 1f, true);

            DrawCompassMark(new Vector2(size.X * 0.5f, size.Y * 0.83f), 18f);
            DrawCompassMark(new Vector2(size.X * 0.5f, size.Y * 0.17f), 11f);

            DrawCrack(
                new Vector2(size.X * 0.05f, size.Y * 0.07f),
                new Vector2(size.X * 0.19f, size.Y * 0.28f));
            DrawCrack(
                new Vector2(size.X * 0.88f, size.Y * 0.04f),
                new Vector2(size.X * 0.77f, size.Y * 0.26f));
            DrawCrack(
                new Vector2(size.X * 0.10f, size.Y * 0.91f),
                new Vector2(size.X * 0.24f, size.Y * 0.73f));
            DrawCrack(
                new Vector2(size.X * 0.92f, size.Y * 0.90f),
                new Vector2(size.X * 0.80f, size.Y * 0.72f));

            // Vinheta simples construída em faixas translúcidas.
            Color vignette = new Color(0f, 0f, 0f, 0.22f);
            float edge = MathF.Min(size.X, size.Y) * 0.045f;
            DrawRect(new Rect2(0f, 0f, size.X, edge), vignette);
            DrawRect(new Rect2(0f, size.Y - edge, size.X, edge), vignette);
            DrawRect(new Rect2(0f, 0f, edge, size.Y), vignette);
            DrawRect(new Rect2(size.X - edge, 0f, edge, size.Y), vignette);
        }

        private void DrawCompassMark(Vector2 center, float radius)
        {
            DrawCircle(center, radius * 0.22f, RuneLine);
            DrawLine(center + Vector2.Up * radius, center + Vector2.Down * radius, RuneLine, 1f, true);
            DrawLine(center + Vector2.Left * radius, center + Vector2.Right * radius, RuneLine, 1f, true);
            DrawLine(
                center + new Vector2(-0.7f, -0.7f) * radius,
                center + new Vector2(0.7f, 0.7f) * radius,
                RuneLineSoft,
                1f,
                true);
            DrawLine(
                center + new Vector2(0.7f, -0.7f) * radius,
                center + new Vector2(-0.7f, 0.7f) * radius,
                RuneLineSoft,
                1f,
                true);
        }

        private void DrawCrack(Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            Vector2 p1 = start + delta * 0.27f + new Vector2(9f, -7f);
            Vector2 p2 = start + delta * 0.53f + new Vector2(-8f, 5f);
            Vector2 p3 = start + delta * 0.76f + new Vector2(7f, 4f);

            DrawLine(start, p1, Crack, 1f, true);
            DrawLine(p1, p2, Crack, 1f, true);
            DrawLine(p2, p3, Crack, 1f, true);
            DrawLine(p3, end, Crack, 1f, true);
            DrawLine(p2, p2 + new Vector2(18f, -10f), Crack, 0.8f, true);
        }
    }
}
