using System.Linq;
using System.Numerics;
using Content.Shared._Impstation.Cosmiccult;
using Content.Shared._Impstation.CosmicCult.Components;
using Content.Shared.Actions;
using Content.Shared.Materials;
using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Impstation.CosmicCult;
public sealed class SharedMonumentSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MonumentComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<MonumentComponent, MaterialEntityInsertedEvent>(OnMaterialInserted);

        SubscribeLocalEvent<MonumentComponent, UpgradeButtonPressedMessage>(OnUpgradeButton);
        SubscribeLocalEvent<MonumentComponent, GlyphSelectedMessage>(OnGlyphSelected);
        SubscribeLocalEvent<MonumentComponent, InfluenceSelectedMessage>(OnInfluenceSelected);
    }

    private void OnUIOpened(Entity<MonumentComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!_uiSystem.IsUiOpen(ent.Owner, MonumentKey.Key))
            return;

        _uiSystem.SetUiState(ent.Owner, MonumentKey.Key, GenerateBuiState(ent.Comp));
    }

    private void OnMaterialInserted(Entity<MonumentComponent> ent, ref MaterialEntityInsertedEvent args)
    {
        var count = TryComp<StackComponent>(args.Material, out var stack) ? stack.Count : 1;
        ent.Comp.AvailableEntropy += count;
        ent.Comp.InfusedEntropy += count;

        _uiSystem.SetUiState(ent.Owner, MonumentKey.Key, GenerateBuiState(ent.Comp));
    }

    #region UI listeners
    private void OnUpgradeButton(Entity<MonumentComponent> ent, ref UpgradeButtonPressedMessage args)
    {
        if (ent.Comp.AvailableEntropy < ent.Comp.EntropyUntilNextStage ||
            ent.Comp.NextMonument == null)
            return;

        var cultists = EntityQuery<CosmicCultComponent>(); // this should probably be CosmicCultRoleComponent, but that means moving it from server
        if (ent.Comp.CrewToConvertNextStage < cultists.Count())
            return;

        ent.Comp.AvailableEntropy -= ent.Comp.EntropyUntilNextStage;

        var newUid = EntityManager.CreateEntityUninitialized(ent.Comp.NextMonument, Transform(ent).Coordinates);
        var newComp = EnsureComp<MonumentComponent>(newUid);
        CloneState(ent.Comp, newComp);

        EntityManager.InitializeAndStartEntity(newUid);
        QueueDel(ent);

        _uiSystem.SetUiState(ent.Owner, MonumentKey.Key, GenerateBuiState(ent.Comp));
    }

    private void OnGlyphSelected(Entity<MonumentComponent> ent, ref GlyphSelectedMessage args)
    {
        // TODO: this needs checks for tier, or mote cost, or whatever you want to do here

        ent.Comp.SelectedGlyph = args.GlyphProtoId; // not sure SelectedGlyph is needed for anything? keeping it here in case

        if (!_prototype.TryIndex(args.GlyphProtoId, out var proto))
            return;

        if (!TryComp<MapGridComponent>(Transform(ent).GridUid, out var grid))
            return;

        var tile = GetGlyphSpawningPoint(ent, grid);
        if (tile == null)
            return;

        Spawn(proto.Entity, _map.ToCenterCoordinates(tile.Value, grid));

        _uiSystem.SetUiState(ent.Owner, MonumentKey.Key, GenerateBuiState(ent.Comp));
    }

    private void OnInfluenceSelected(Entity<MonumentComponent> ent, ref InfluenceSelectedMessage args)
    {
        var query = EntityQueryEnumerator<CosmicCultComponent>(); // this should probably be CosmicCultRoleComponent, but that means moving it from server

        if (!_prototype.TryIndex(args.InfluenceProtoId, out var proto))
            return;

        if (ent.Comp.AvailableEntropy < proto.Cost)
            return;

        while (query.MoveNext(out var uid, out _))
        {
            _action.AddAction(uid, proto.Action);
        }

        ent.Comp.AvailableEntropy -= proto.Cost;
        ent.Comp.UnlockedInfluences.Remove(args.InfluenceProtoId);

        _uiSystem.SetUiState(ent.Owner, MonumentKey.Key, GenerateBuiState(ent.Comp));
    }
    #endregion

    #region Helper functions
    private MonumentBuiState GenerateBuiState(MonumentComponent comp)
    {
        return new MonumentBuiState(
            comp.InfusedEntropy,
            comp.AvailableEntropy,
            comp.EntropyUntilNextStage,
            comp.CrewToConvertNextStage,
            comp.PercentageComplete,
            comp.SelectedGlyph,
            comp.UnlockedInfluences
        );
    }

    private static void CloneState(MonumentComponent cloneFrom, MonumentComponent cloneTo)
    {
        // TODO: replace with a better method of copying components
        // https://github.com/space-wizards/RobustToolbox/pull/5654
        cloneTo.InfusedEntropy = cloneFrom.InfusedEntropy;
        cloneTo.AvailableEntropy = cloneFrom.AvailableEntropy;
        cloneTo.EntropyUntilNextStage = cloneFrom.EntropyUntilNextStage;
        cloneTo.CrewToConvertNextStage = cloneFrom.CrewToConvertNextStage;
        cloneTo.PercentageComplete = cloneFrom.PercentageComplete;
        cloneTo.SelectedGlyph = cloneFrom.SelectedGlyph;
        cloneTo.UnlockedInfluences = cloneFrom.UnlockedInfluences;
    }

    private TileRef? GetGlyphSpawningPoint(Entity<MonumentComponent> ent, MapGridComponent grid)
    {
        var xform = Transform(ent);

        if (xform.GridUid == null)
            return null;

        var localPosition = xform.LocalPosition;
        var tileRefs = _map.GetLocalTilesIntersecting(
                xform.GridUid.Value,
                grid,
                new Box2(localPosition + new Vector2(-1, -1), localPosition + new Vector2(1, -1)))
            .ToList();

        if (tileRefs.Count == 0)
            return null;

        // need to move CosmicGlyph to shared so we don't end up stacking glyphs
        TileRef? result = null;
        while (result == null)
        {
            if (tileRefs.Count == 0)
                break;

            var tileRef = _random.Pick(tileRefs);
            var valid = true;

            // need to move CosmicGlyph to shared so this can be uncommented to prevent glyph stacking
            /*
            foreach (var tileEnt in _map.GetAnchoredEntities(xform.GridUid.Value, grid, tileRef.GridIndices))
            {
                if (!HasComp<CosmicGlyphComponent>(tileEnt))
                    continue;

                valid = false;
                break;
            }
            */

            if (!valid)
            {
                tileRefs.Remove(tileRef);
                continue;
            }

            result = tileRef;
        }

        return result;
    }
    #endregion
}
