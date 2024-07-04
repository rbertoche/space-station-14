using Robust.Shared.Audio;

namespace Content.Shared._EstacaoPirata.Cards.Deck;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class CardDeckComponent : Component
{
    [DataField("shuffleSound")]
    public SoundSpecifier ShuffleSound = new SoundPathSpecifier("/Audio/Items/Paper/paper_scribble1.ogg"); //REMEMBER TO CHANGE IT LATER!!!!!

    public int CardLimit = 5;
}
