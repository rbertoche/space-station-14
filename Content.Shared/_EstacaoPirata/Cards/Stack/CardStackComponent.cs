using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._EstacaoPirata.Stack.Cards;

/// <summary>
/// This is used for holding the prototype ids of the cards in the stack or hand.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]

public sealed partial class CardStackComponent : Component
{
    [DataField("content")]
    public List<EntProtoId> InitialContent = [];

    /// <summary>
    /// The containers that contain the items held in the stack
    /// </summary>
    [ViewVariables]
    public Container ItemContainer = default!;

    /// <summary>
    /// The list EntityUIds of Cards
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntityUid> Cards = [];
}

[Serializable, NetSerializable]
public sealed class CardStackInitiatedEvent(NetEntity cardStack, CardStackComponent? component) : EntityEventArgs
{
    public NetEntity CardStack = cardStack;
}

/// <summary>
/// This gets Updated when new cards are added or removed from the stack
/// </summary>

public sealed class CardStackQuantityChangeEvent : EntityEventArgs
{
    public StackQuantityChangeType Type;
    public EntityUid Card;
}

public enum StackQuantityChangeType
{
    Added,
    Removed
}




public sealed class CardStackReorderedEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class CardStackFlippedEvent(NetEntity cardStack) : EntityEventArgs
{
    public NetEntity CardStack = cardStack;
}



