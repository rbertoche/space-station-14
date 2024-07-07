using System.Linq;
using Content.Shared._EstacaoPirata.Cards.Card;
using Content.Shared._EstacaoPirata.Cards.Stack;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Shared.Network;

namespace Content.Shared._EstacaoPirata.Cards.Hand;

/// <summary>
/// This handles...
/// </summary>
public sealed class CardHandSystem : EntitySystem
{
    const string CardHandBaseName = "CardHandBase";

    [Dependency] private readonly CardStackSystem _cardStack = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly CardStackSystem _cardStackSystem = default!;


    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<CardComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CardHandComponent, CardHandDrawMessage>(OnCardDraw);
        SubscribeLocalEvent<CardHandComponent, CardStackQuantityChangeEvent>(OnStackQuantityChange);
        //SubscribeLocalEvent<CardComponent, GetVerbsEvent<AlternativeVerb>>(AddTurnOnVerb);
    }

    private void OnStackQuantityChange(EntityUid uid, CardHandComponent comp,  CardStackQuantityChangeEvent args)
    {
        if (_net.IsClient)
            return;
        if (!TryComp(uid, out CardStackComponent? stack))
            return;
        _cardStackSystem.FlipAllCards(uid, stack, false);
    }

    private void OnCardDraw(EntityUid uid, CardHandComponent comp, CardHandDrawMessage args)
    {
        if (!TryComp(uid, out CardStackComponent? stack))
            return;
        if (!_cardStackSystem.TryRemoveCard(uid, GetEntity(args.Card), stack))
            return;

        _hands.TryPickupAnyHand(args.Actor, GetEntity(args.Card));


        if (stack.Cards.Count != 1)
            return;
        var lastCard = stack.Cards.Last();
        if (!_cardStackSystem.TryRemoveCard(uid, lastCard, stack))
            return;
        _hands.TryPickupAnyHand(args.Actor, lastCard);

    }

    private void OnInteractUsing(EntityUid uid, CardComponent comp, InteractUsingEvent args)
    {
        if (TryComp(args.Used, out CardComponent? usedComp) && TryComp(args.Target, out CardComponent? targetComp))
        {
            TrySetupHandOfCards(args.User, args.Used, usedComp, args.Target, targetComp);
        }
    }

    private void TrySetupHandOfCards(EntityUid user, EntityUid card, CardComponent comp, EntityUid target, CardComponent targetComp)
    {
        if (_net.IsClient)
            return;
        var cardHand = Spawn(CardHandBaseName, Transform(card).Coordinates);
        if (!TryComp(cardHand, out CardStackComponent? stack))
            return;
        if (!_cardStack.TryInsertCard(cardHand, card, stack) || !_cardStack.TryInsertCard(cardHand, target, stack))
            return;
        if (!_hands.TryPickupAnyHand(user, cardHand))
            return;
        _cardStackSystem.FlipAllCards(cardHand, stack, false);

    }
}
