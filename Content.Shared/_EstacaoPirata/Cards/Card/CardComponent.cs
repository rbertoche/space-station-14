using Robust.Shared.GameStates;

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
    [DataField("frontState", readOnly: true)]
    public string? FrontState;
    [DataField("flipped", readOnly: true), AutoNetworkedField]
    public bool Flipped = false;

    public sealed class CardFlipUpdatedEvent : EntityEventArgs
    {
    }
}
