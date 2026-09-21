using Godot;

namespace TicTacToeRoguelike.Content.Runes
{
    [GlobalClass]
    public sealed partial class IndependentMultiplierRuneDefinitionResource :
        RuneDefinitionResource
    {
        [Export]
        public double IndependentMultiplier { get; set; } = 1.20d;
    }
}
