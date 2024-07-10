using Content.Shared._EstacaoPirata.Cards.Card;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Client._EstacaoPirata.Cards.Card;

/// <summary>
/// This handles...
/// </summary>
public sealed class CardSystem : EntitySystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<CardComponent, ComponentStartup>(OnComponentStartupEvent);
        SubscribeNetworkEvent<CardFlipUpdatedEvent>(OnFlip);
    }

    private void OnComponentStartupEvent(EntityUid uid, CardComponent comp, ComponentStartup args)
    {
        if (!TryComp(uid, out SpriteComponent? spriteComponent))
            return;

        var state = spriteComponent.LayerGetState(0);

        comp.FrontState = state.Name;
        comp.BackState ??= state.Name;
        Dirty(uid, comp);
        UpdateSprite(uid, comp);
    }

    private void OnFlip(CardFlipUpdatedEvent args)
    {
        if (!TryComp(GetEntity(args.Card), out CardComponent? comp))
            return;
        UpdateSprite(GetEntity(args.Card), comp);
    }

    private void UpdateSprite(EntityUid uid, CardComponent comp)
    {
        var newState = comp.Flipped ? comp.BackState : comp.FrontState;
        if (TryComp(uid, out SpriteComponent? spriteComponent))
        {
            spriteComponent.LayerSetState(0, newState ?? "");
        }
    }
}
