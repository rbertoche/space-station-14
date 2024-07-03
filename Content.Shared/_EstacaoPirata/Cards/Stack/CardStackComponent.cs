using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

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
