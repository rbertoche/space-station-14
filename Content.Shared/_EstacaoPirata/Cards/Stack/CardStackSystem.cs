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

        SubscribeLocalEvent<CardStackComponent, InteractUsingEvent>(OnInteractUsing);
    }

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
        RaiseNetworkEvent(new CardStackComponent.CardStackInitiatedEvent(GetNetEntity(uid), comp));
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
        RaiseLocalEvent(uid, new CardStackComponent.CardStackUpdatedEvent());
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
        RaiseLocalEvent(uid, new CardStackComponent.CardStackUpdatedEvent());
        return true;
    }

    public bool ShuffleCards(EntityUid uid, CardStackComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        _random.Shuffle(comp.Cards);

        Dirty(uid, comp);
        RaiseLocalEvent(uid, new CardStackComponent.CardStackUpdatedEvent());
        return true;
    }

    // It seems the cards don't get removed if this event is not subscribed... strange right? thanks again bin system
    private void OnEntRemoved(EntityUid uid, CardStackComponent component, EntRemovedFromContainerMessage args)
    {
        component.Cards.Remove(args.Entity);
    }



    // Adds Cards to stack or Joins two stack
    private void OnInteractUsing(EntityUid uid, CardStackComponent firstStack, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // This checks if the user is adding a card to a stack
        if (TryComp(args.Used, out CardComponent? _))
        {
            TryInsertCard(uid, (EntityUid)args.Used);
            RaiseLocalEvent(uid, new CardStackComponent.CardStackCardAddedEvent{Card = args.Used});
            args.Handled = true;
            return;
        }

        // This checks if the user is joining two stacks
        if (_net.IsClient)
            return;

        if (!TryComp(args.Used, out CardStackComponent? secondStack))
            return;

        foreach (var card in secondStack.Cards.ToList())
        {
            _container.Remove(card, secondStack.ItemContainer);
            _container.Insert(card, firstStack.ItemContainer);
        }

        firstStack.Cards.AddRange(secondStack.Cards);
        secondStack.Cards.Clear();
        Dirty(args.Target, firstStack);
        Dirty(args.Used, secondStack);

        _entityManager.DeleteEntity(args.Used);

        RaiseLocalEvent(uid, new CardStackComponent.CardStackUpdatedEvent());
        args.Handled = true;
    }




}
