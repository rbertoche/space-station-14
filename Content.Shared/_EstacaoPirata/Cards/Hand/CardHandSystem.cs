using System.Linq;
using Content.Shared._EstacaoPirata.Cards.Card;
using Content.Shared._EstacaoPirata.Cards.Stack;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

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
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;



    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<CardComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CardHandComponent, CardHandDrawMessage>(OnCardDraw);
        SubscribeLocalEvent<CardHandComponent, CardStackQuantityChangeEvent>(OnStackQuantityChange);
        SubscribeLocalEvent<CardHandComponent, GetVerbsEvent<AlternativeVerb>>(OnAlternativeVerb);
    }

    private void OnStackQuantityChange(EntityUid uid, CardHandComponent comp,  CardStackQuantityChangeEvent args)
    {
        if (_net.IsClient)
            return;
        if (!TryComp(uid, out CardStackComponent? stack))
            return;
        _cardStack.FlipAllCards(uid, stack, false);
    }

    private void OnCardDraw(EntityUid uid, CardHandComponent comp, CardHandDrawMessage args)
    {
        if (!TryComp(uid, out CardStackComponent? stack))
            return;
        if (!_cardStack.TryRemoveCard(uid, GetEntity(args.Card), stack))
            return;

        _hands.TryPickupAnyHand(args.Actor, GetEntity(args.Card));


        if (stack.Cards.Count != 1)
            return;
        var lastCard = stack.Cards.Last();
        if (!_cardStack.TryRemoveCard(uid, lastCard, stack))
            return;
        _hands.TryPickupAnyHand(args.Actor, lastCard);

    }

    private void OpenHandMenu(EntityUid user, EntityUid hand)
    {
        if (!TryComp<ActorComponent>(user, out var actor))
            return;

        _ui.OpenUi(hand, CardUiKey.Key, actor.PlayerSession);

    }

    private void OnAlternativeVerb(EntityUid uid, CardHandComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        args.Verbs.Add(new AlternativeVerb()
        {
            Act = () => OpenHandMenu(args.User, uid),
            Text = Loc.GetString("cards-verb-pickcard"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/die.svg.192dpi.png")),
            Priority = 3
        });
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
        _cardStack.FlipAllCards(cardHand, stack, false);

    }
}
