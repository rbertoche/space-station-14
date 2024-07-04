using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._EstacaoPirata.Cards.Card;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CardComponent : Component
{
    /// <summary>
    /// The back of the card
    /// </summary>
    [DataField("backState", readOnly: true)]
    public string? BackState;

    /// <summary>
    /// The front of the card
    /// </summary>
    [DataField("frontState", readOnly: true)]
    public string? FrontState;

    /// <summary>
    /// If it is currently flipped. This is used to update sprite and name.
    /// </summary>
    [DataField("flipped", readOnly: true), AutoNetworkedField]
    public bool Flipped = false;


    /// <summary>
    /// The name of the card.
    /// </summary>
    [DataField("name", readOnly: true), AutoNetworkedField]
    public string Name = "";

    public sealed class CardFlipUpdatedEvent : EntityEventArgs
    {
    }
}
