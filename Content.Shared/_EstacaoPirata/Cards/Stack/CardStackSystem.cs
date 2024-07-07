using System.Linq;
using Content.Shared._EstacaoPirata.Cards.Card;
using Content.Shared.Interaction;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared._EstacaoPirata.Stack.Cards;

/// <summary>
/// This handles...
/// </summary>
///
public sealed class CardStackSystem : EntitySystem
{
    public const string ContainerId = "cardstack-container";

    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;


    /// <inheritdoc/>
    public override void Initialize()
    {
        // Pretty much a rip-off of the BinSystem
        SubscribeLocalEvent<CardStackComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CardStackComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CardStackComponent, EntRemovedFromContainerMessage>(OnEntRemoved);

        SubscribeLocalEvent<InteractUsingEvent>(OnInteractUsing);
    }



    public bool TryRemoveCard(EntityUid uid, EntityUid card, CardStackComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        if (!TryComp(card, out CardComponent? cardComponent))
            return false;

        _container.Remove(card, comp.ItemContainer);
        comp.Cards.Remove(card);

        Dirty(uid, comp);

        // Prevents prediction ruining things
        if (_net.IsServer && comp.Cards.Count <= 0)
        {
            _entityManager.DeleteEntity(uid);
        }
        RaiseLocalEvent(uid, new CardStackQuantityChangeEvent{Card = card, Type = StackQuantityChangeType.Removed});
        return true;
    }

    public bool TryInsertCard(EntityUid uid, EntityUid card, CardStackComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        if (!TryComp(card, out CardComponent? cardComponent))
            return false;

        _container.Insert(card, comp.ItemContainer);
        comp.Cards.Add(card);

        Dirty(uid, comp);
        RaiseLocalEvent(uid, new CardStackQuantityChangeEvent{Card = card, Type = StackQuantityChangeType.Added});
        return true;
    }

    public bool ShuffleCards(EntityUid uid, CardStackComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        _random.Shuffle(comp.Cards);

        Dirty(uid, comp);
        RaiseLocalEvent(uid, new CardStackReorderedEvent());
        return true;
    }

    /// <summary>
    /// Server-Side only method to flip all cards within a stack. This starts CardFlipUpdatedEvent and CardStackFlippedEvent event
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="comp"></param>
    /// <param name="isFlipped">If null, all cards will just invert direction, if it contains a value, then all cards will receive that value</param>
    /// <returns></returns>
    public bool FlipAllCards(EntityUid uid, CardStackComponent? comp = null, bool? isFlipped = null)
    {
        if (_net.IsClient)
            return false;
        if (!Resolve(uid, ref comp))
            return false;
        foreach (var card in comp.Cards)
        {
            if (!TryComp(card, out CardComponent? cardComponent))
                continue;


            cardComponent.Flipped = isFlipped?? !cardComponent.Flipped;

            Dirty(card, cardComponent);
            RaiseNetworkEvent(new CardFlipUpdatedEvent(GetNetEntity(card)));
        }

        RaiseNetworkEvent(new CardStackFlippedEvent(GetNetEntity(uid)));
        return true;
    }


    public bool TryJoinStacks(EntityUid firstStack, EntityUid secondStack, CardStackComponent? firstComp = null, CardStackComponent? secondComp = null)
    {
        if (!Resolve(firstStack, ref firstComp) || !Resolve(secondStack, ref secondComp))
            return false;

        foreach (var card in secondComp.Cards.ToList())
        {
            _container.Remove(card, secondComp.ItemContainer);
            secondComp.Cards.Remove(card);
            firstComp.Cards.Add(card);
            _container.Insert(card, firstComp.ItemContainer);
        }
        Dirty(firstStack, firstComp);

        _entityManager.DeleteEntity(secondStack);

        RaiseLocalEvent(firstStack, new CardStackQuantityChangeEvent{Card = firstStack, Type = StackQuantityChangeType.Added});
        return true;
    }

    #region EventHandling

    private void OnStartup(EntityUid uid, CardStackComponent component, ComponentStartup args)
    {
        component.ItemContainer = _container.EnsureContainer<Container>(uid, ContainerId);
    }

    private void OnMapInit(EntityUid uid, CardStackComponent comp, MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        var coordinates = Transform(uid).Coordinates;
        foreach (var id in comp.InitialContent)
        {
            var ent = Spawn(id, coordinates);
            if (!TryInsertCard(uid, ent, comp))
            {
                Log.Error($"Entity {ToPrettyString(ent)} was unable to be initialized into stack {ToPrettyString(uid)}");
                return;
            }
            comp.Cards.Add(ent);
        }
        RaiseNetworkEvent(new CardStackInitiatedEvent(GetNetEntity(uid), comp));
    }


    // It seems the cards don't get removed if this event is not subscribed... strange right? thanks again bin system
    private void OnEntRemoved(EntityUid uid, CardStackComponent component, EntRemovedFromContainerMessage args)
    {
        component.Cards.Remove(args.Entity);
    }


    private void OnInteractUsing(InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (_net.IsClient)
            return;

        // This checks if the user is using an item with Stack component
        if (TryComp(args.Used, out CardStackComponent? usedStack))
        {
            // If the target is a card, then it will insert the card into the stack
            if (TryComp(args.Target, out CardComponent? card))
            {
                TryInsertCard(args.Used, args.Target);
                args.Handled = true;
                return;
            }

            // If instead, the target is a stack, then it will join the two stacks
            if (!TryComp(args.Target, out CardStackComponent? firstStack))
                return;
            TryJoinStacks(args.Target, args.Used, firstStack, usedStack);

        }

        // This handles the reverse case, where the user is using a card and inserting it to a stack
        else if (TryComp(args.Target, out CardStackComponent? targetStack))
        {
            if (TryComp(args.Used, out CardComponent? card))
            {
                TryInsertCard(args.Target, args.Used);
                args.Handled = true;
                return;
            }
        }


        args.Handled = true;
    }



    #endregion


}
