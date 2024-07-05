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

    [Serializable, NetSerializable]
    public sealed class CardStackInitiatedEvent(NetEntity cardStack, CardStackComponent? component) : EntityEventArgs
    {
        public NetEntity CardStack = cardStack;
    }
    public sealed class CardStackUpdatedEvent : EntityEventArgs
    {
    }

    // For when a card is added to the stack
    public sealed class CardStackCardAddedEvent : EntityEventArgs
    {
        public EntityUid Card;
    }
}
