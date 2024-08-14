using Content.Server.GameTicking.Rules.VariationPass.Components;

namespace Content.Server.GameTicking.Rules.VariationPass;

/// <summary>
/// Assigns a random announcer to the shift.
/// </summary>
public sealed class AnnouncerVariationPassSystem : VariationPassSystem<AnnouncerVariationPassComponent>
{
    protected override void ApplyVariation(Entity<AnnouncerVariationPassComponent> ent, ref StationVariationPassEvent args)
    {
        
    }
}
