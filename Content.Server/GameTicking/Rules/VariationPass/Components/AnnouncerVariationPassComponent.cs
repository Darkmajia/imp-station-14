namespace Content.Server.GameTicking.Rules.VariationPass.Components;

/// <summary>
/// Assigns a random announcer to the shift.
/// </summary>
[RegisterComponent]
public sealed partial class AnnouncerVariationPassComponent : Component
{
    /// <summary>
    /// Chance for the intern to be the announcer.
    /// </summary>
    public float InternChance = 0.05f;
}
