using System.Linq;
using System.Numerics;
using Content.Shared._EstacaoPirata.Cards.Deck;
using Content.Shared._EstacaoPirata.Stack.Cards;
using Robust.Client.GameObjects;

namespace Content.Client._EstacaoPirata.Cards.Deck;

/// <summary>
/// This handles...
/// </summary>
public sealed class CardDeckSystem : EntitySystem
{


    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<CardDeckComponent, ComponentStartup>(OnComponentStartupEvent);
        SubscribeLocalEvent<CardDeckComponent, CardStackComponent.CardStackUpdatedEvent>(OnStackUpdate);
        SubscribeNetworkEvent<CardStackComponent.CardStackInitiatedEvent>(OnStackStart);
    }

    private void UpdateSprite(EntityUid uid, CardDeckComponent comp)
    {
        if (!TryComp(uid, out SpriteComponent? sprite))
            return;

        if (!TryComp(uid, out CardStackComponent? cardStack))
            return;
        Log.Debug("Updating sprite...????");

        if (sprite.AllLayers.Count() < comp.CardLimit)
        {
            if (!sprite.TryGetLayer(0, out var firstLayer))
                return;

            for (var i = 0; i < comp.CardLimit - 1; i++)
            {
                sprite.AddLayer(firstLayer.State, i);
            }
        }

        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            sprite.LayerSetVisible(i, true);
        }



        var j = 0;
        // Show the last 5 cards
        foreach (var card in cardStack.Cards.TakeLast(comp.CardLimit))
        {
            if (!TryComp(card, out SpriteComponent? cardSprite))
                continue;
            if (!cardSprite.TryGetLayer(0, out var layer))
                continue;
            sprite.LayerSetTexture(j, layer.Texture);
            sprite.LayerSetState(j, layer.State);
            sprite.LayerSetRotation(j, Angle.FromDegrees(90));
            sprite.LayerSetOffset(j, new Vector2(0, (float)(0.02 * j)));
            sprite.LayerSetScale(j, new Vector2(0.7f, 0.7f));
            j++;

        }

        var cardsQuantity = cardStack.Cards.Count;
        var layersQuantity = sprite.AllLayers.ToList().Count;
        if (cardsQuantity < layersQuantity - 1)
        {
            for (int k = 0; k < (layersQuantity - cardsQuantity); k++)
            {
                sprite.LayerSetVisible(layersQuantity - k - 1, false);
            }
        }
    }

    private void OnComponentStartupEvent(EntityUid uid, CardDeckComponent comp, ComponentStartup args)
    {

        UpdateSprite(uid, comp);
    }
    private void OnStackUpdate(EntityUid uid, CardDeckComponent comp, CardStackComponent.CardStackUpdatedEvent args)
    {
        UpdateSprite(uid, comp);
    }

    private void OnStackStart(CardStackComponent.CardStackInitiatedEvent args)
    {
        var entity = GetEntity(args.CardStack);
        if (!TryComp(entity, out CardDeckComponent? comp))
            return;

        UpdateSprite(entity, comp);
    }
}
