using System;
using System.Linq;
using System.Numerics;
using Content.Client._EstacaoPirata.Cards.Hand.UI;
using Content.Shared._EstacaoPirata.Cards.Hand;
using Content.Shared._EstacaoPirata.Cards.Stack;
using Content.Shared.Verbs;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Client._EstacaoPirata.Cards.Hand;

/// <summary>
/// This handles...
/// </summary>
public sealed class CardHandSystem : EntitySystem
{


    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<CardHandComponent, ComponentStartup>(OnComponentStartupEvent);
        SubscribeNetworkEvent<CardStackInitiatedEvent>(OnStackStart);
        SubscribeNetworkEvent<CardStackQuantityChangeEvent>(OnStackUpdate);

    }

    private void UpdateSprite(EntityUid uid, CardHandComponent comp)
    {
        if (!TryComp(uid, out SpriteComponent? sprite))
            return;

        if (!TryComp(uid, out CardStackComponent? cardStack))
            return;

        var cardCount = Math.Min(cardStack.Cards.Count, comp.CardLimit);

        //inserts Missing Layers
        if (sprite.AllLayers.Count() < cardCount)
        {
            if (!sprite.TryGetLayer(0, out var firstLayer))
                return;
            for (var i = sprite.AllLayers.Count(); i < cardCount; i++)
            {
                sprite.AddLayer(firstLayer.State, i);
            }
        }
        //Removes extra layers
        else if (sprite.AllLayers.Count() > cardCount)
        {
            for (var i = cardCount; i < sprite.AllLayers.Count(); i++)
            {
                sprite.RemoveLayer(i);
            }
        }

        var j = 0;
        var intervalAngle = comp.Angle / (cardCount-1);
        var intervalSize = comp.XOffset / (cardCount - 1);
        foreach (var card in cardStack.Cards)
        {
            if (!TryGetCardLayer(card, out var layer) || layer == null)
                return;

            var angle = (-(comp.Angle/2)) + j * intervalAngle;
            var x = (-(comp.XOffset / 2)) + j * intervalSize;
            var y = -(x * x) + 0.10f;

            sprite.LayerSetVisible(0, true);
            sprite.LayerSetTexture(j, layer.Texture);
            sprite.LayerSetState(j, layer.State);
            sprite.LayerSetRotation(j, Angle.FromDegrees(-angle));
            sprite.LayerSetOffset(j, new Vector2(x, y));
            sprite.LayerSetScale(j, new Vector2(comp.Scale, comp.Scale));
            j++;
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

    private void OnStackUpdate(CardStackQuantityChangeEvent args)
    {
        if (!TryComp(GetEntity(args.Stack), out CardHandComponent? comp))
            return;
        UpdateSprite(GetEntity(args.Stack), comp);
    }

    private void OnStackStart(CardStackInitiatedEvent args)
    {
        var entity = GetEntity(args.CardStack);
        if (!TryComp(entity, out CardHandComponent? comp))
            return;

        UpdateSprite(entity, comp);
    }
    private void OnComponentStartupEvent(EntityUid uid, CardHandComponent comp, ComponentStartup args)
    {

        UpdateSprite(uid, comp);
    }


}
