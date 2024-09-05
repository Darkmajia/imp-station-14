using Content.Shared.ActionBlocker;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Storage.EntitySystems;

namespace Content.Shared.Inventory;

public sealed class PeekOnlySystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedStorageSystem _storageSystem = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<PeekOnlyComponent, BeingEquippedAttemptEvent>(OnBeingEquipped);
        SubscribeLocalEvent<PeekOnlyComponent, BeingUnequippedAttemptEvent>(OnBeingUnequipped);
    }

    private void OnBeingEquipped(Entity<PeekOnlyComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (TryComp<ClothingComponent>(ent, out var clothing) && (clothing.Slots & args.SlotFlags) == SlotFlags.NONE)
            return;

        if (args.Equipee != args.EquipTarget)
            args.Cancel();
    }

    private void OnBeingUnequipped(Entity<PeekOnlyComponent> ent, ref BeingUnequippedAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (TryComp<ClothingComponent>(ent, out var clothing) && (clothing.Slots & args.SlotFlags) == SlotFlags.NONE)
            return;

        args.Cancel();
    }
}
