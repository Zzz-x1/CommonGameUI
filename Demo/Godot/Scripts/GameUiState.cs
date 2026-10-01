namespace HierarchyDemo;

/// <summary>Project-owned session model. It contains UI data only, no gameplay.</summary>
public sealed class GameUiState
{
    public double RenderScale { get; set; } = 65;
    public bool Shadows { get; set; } = true;
    public double MasterVolume { get; set; } = 70;
    public bool Music { get; set; } = true;
    public double CameraSensitivity { get; set; } = 45;
    public bool InvertCamera { get; set; }
    public HashSet<string> Favorites { get; } = new() { "Oakheart Bow", "Moonstone" };
}
