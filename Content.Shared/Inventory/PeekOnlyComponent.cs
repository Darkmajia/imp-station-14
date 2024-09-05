using Robust.Shared.GameStates;

namespace Content.Shared.Inventory;

/// <summary>
/// This is used for an item that can only be peeked inside, and cannot be removed after being equipped.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(PeekOnlySystem))]
public sealed partial class PeekOnlyComponent : Component
{
}
