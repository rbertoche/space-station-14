using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared._EstacaoPirata.Cards.Deck;
using Content.Shared._EstacaoPirata.Cards.Stack;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Client._EstacaoPirata.Cards.Deck;

/// <summary>
/// This handles...
/// </summary>
public sealed class CardDeckSystem : EntitySystem
{
    private readonly Dictionary<Entity<CardDeckComponent>, int> _notInitialized = [];


    /// <inheritdoc/>
    public override void Initialize()
    {
        UpdatesOutsidePrediction = false;
        SubscribeLocalEvent<CardDeckComponent, ComponentStartup>(OnComponentStartupEvent);
        SubscribeNetworkEvent<CardStackInitiatedEvent>(OnStackStart);
        SubscribeNetworkEvent<CardStackQuantityChangeEvent>(OnStackUpdate);
        SubscribeNetworkEvent<CardStackReorderedEvent>(OnReorder);
        SubscribeNetworkEvent<CardStackFlippedEvent>(OnStackFlip);
        SubscribeLocalEvent<CardDeckComponent, AppearanceChangeEvent>(OnAppearanceChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Lazy way to make sure the sprite starts correctly
        foreach (var kv in _notInitialized)
        {
            var ent = kv.Key;

            if (kv.Value >= 5)
            {
                _notInitialized.Remove(ent);
                continue;
            }

            _notInitialized[ent] = kv.Value + 1;

            if (!TryComp(ent.Owner, out CardStackComponent? stack) || stack.Cards.Count <= 0)
                continue;


            // If the card was STILL not initialized, we skip it
            if (!TryGetCardLayer(stack.Cards.Last(), out var _))
                continue;

            // If cards were correctly initialized, we update the sprite
            UpdateSprite(ent.Owner, ent.Comp);
            _notInitialized.Remove(ent);
        }

    }


    // This is executed only if there are no available layers to work with
    private static void SetupSpriteLayers(EntityUid _, CardDeckComponent comp, SpriteComponent sprite, int layersQuantity)
    {
        if (!sprite.TryGetLayer(0, out var firstLayer))
            return;

        for (var i = layersQuantity; i < comp.CardLimit; i++)
        {
            sprite.AddLayer(firstLayer.State, i);
        }
    }

    private bool TryGetCardLayer(EntityUid card, out SpriteComponent.Layer? layer)
    {
        layer = null;
        if (!TryComp(card, out SpriteComponent? cardSprite))
            return false;

        if (!cardSprite.TryGetLayer(0, out var l))
            return false;

        layer = l;
        return true;
    }

    private void UpdateSprite(EntityUid uid, CardDeckComponent comp)
    {
        if (!TryComp(uid, out SpriteComponent? sprite))
            return;

        if (!TryComp(uid, out CardStackComponent? cardStack))
            return;


        // Prevents error appearing at spawnMenu
        if (cardStack.Cards.Count <= 0 || !TryGetCardLayer(cardStack.Cards.Last(), out var cardlayer) ||
            cardlayer == null)
        {
            _notInitialized[(uid, comp)] = 0;
            return;
        }

        // This sets up the layers if they are not initialized
        if (sprite.AllLayers.Count() < comp.CardLimit)
        {
            SetupSpriteLayers(uid, comp, sprite, sprite.AllLayers.Count());
        }


        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            sprite.LayerSetVisible(i, true);
        }

        var j = 0;
        // Shows the last 5 cards
        foreach (var card in cardStack.Cards.TakeLast(comp.CardLimit))
        {
            if (!TryGetCardLayer(card, out var layer) || layer == null)
                return;
            sprite.LayerSetTexture(j, layer.Texture);
            sprite.LayerSetState(j, layer.State);
            sprite.LayerSetRotation(j, Angle.FromDegrees(90));
            sprite.LayerSetOffset(j, new Vector2(0, (comp.YOffset * j)));
            sprite.LayerSetScale(j, new Vector2(comp.Scale, comp.Scale));
            j++;
        }

        var cardsQuantity = cardStack.Cards.Count;
        var layersQuantity = sprite.AllLayers.ToList().Count;

        if (cardsQuantity >= layersQuantity - 1)
            return;

        for (var k = 0; k < (layersQuantity - cardsQuantity); k++)
        {
            sprite.LayerSetVisible(layersQuantity - k - 1, false);
        }
    }

    private void OnStackUpdate(CardStackQuantityChangeEvent args)
    {
        if (!TryComp(GetEntity(args.Stack), out CardDeckComponent? comp))
            return;
        UpdateSprite(GetEntity(args.Stack), comp);
    }

    private void OnStackFlip(CardStackFlippedEvent args)
    {
        if (!TryComp(GetEntity(args.CardStack), out CardDeckComponent? comp))
            return;
        UpdateSprite(GetEntity(args.CardStack), comp);
    }

    private void OnReorder(CardStackReorderedEvent args)
    {
        if (!TryComp(GetEntity(args.Stack), out CardDeckComponent? comp))
            return;
        UpdateSprite(GetEntity(args.Stack), comp);
    }

    private void OnAppearanceChanged(EntityUid uid, CardDeckComponent comp, AppearanceChangeEvent args)
    {
        UpdateSprite(uid, comp);
    }
    private void OnComponentStartupEvent(EntityUid uid, CardDeckComponent comp, ComponentStartup args)
    {

        UpdateSprite(uid, comp);
    }


    private void OnStackStart(CardStackInitiatedEvent args)
    {
        var entity = GetEntity(args.CardStack);
        if (!TryComp(entity, out CardDeckComponent? comp))
            return;

        UpdateSprite(entity, comp);
    }

}
