using System.Linq;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Xenonids.Announce;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Systems;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Prototypes;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Xenonids.Evolution;

public sealed class XenoEvolutionSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ClimbSystem _climb = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly SharedGameTicker _gameTicker = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;
    [Dependency] private readonly SharedXenoAnnounceSystem _xenoAnnounce = default!;
    [Dependency] private readonly SharedXenoHiveSystem _xenoHive = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    private TimeSpan _evolutionPointsRequireOvipositorAfter;
    private TimeSpan _evolutionAccumulatePointsBefore;

    private readonly HashSet<EntityUid> _climbable = new();
    private readonly HashSet<EntityUid> _doors = new();
    private readonly HashSet<EntityUid> _intersecting = new();

    private EntityQuery<MobStateComponent> _mobStateQuery;

    public override void Initialize()
    {
        _mobStateQuery = GetEntityQuery<MobStateComponent>();

        SubscribeLocalEvent<XenoDevolveComponent, XenoOpenDevolveActionEvent>(OnXenoOpenDevolveAction);

        SubscribeLocalEvent<XenoEvolutionComponent, MapInitEvent>(OnXenoEvolveMapInit);
        SubscribeLocalEvent<XenoEvolutionComponent, XenoOpenEvolutionsActionEvent>(OnXenoEvolveAction);
        SubscribeLocalEvent<XenoEvolutionComponent, XenoEvolutionDoAfterEvent>(OnXenoEvolveDoAfter);
        SubscribeLocalEvent<XenoEvolutionComponent, NewXenoEvolvedEvent>(OnXenoEvolutionNewEvolved);
        SubscribeLocalEvent<XenoEvolutionComponent, XenoDevolvedEvent>(OnXenoEvolutionDevolved);

        SubscribeLocalEvent<XenoNewlyEvolvedComponent, PreventCollideEvent>(OnNewlyEvolvedPreventCollide);

        SubscribeLocalEvent<XenoEvolutionGranterComponent, NewXenoEvolvedEvent>(OnGranterEvolved);

        SubscribeLocalEvent<XenoOvipositorChangedEvent>(OnOvipositorChanged);

        Subs.BuiEvents<XenoEvolutionComponent>(XenoEvolutionUIKey.Key,
            subs =>
            {
                subs.Event<XenoEvolveBuiMsg>(OnXenoEvolveBui);
                subs.Event<XenoStrainBuiMsg>(OnXenoStrainBui);
            });

        Subs.BuiEvents<XenoDevolveComponent>(XenoDevolveUIKey.Key,
            subs =>
            {
                subs.Event<XenoDevolveBuiMsg>(OnXenoDevolveBui);
            });

        Subs.CVar(_config, RMCCVars.RMCEvolutionPointsRequireOvipositorMinutes, v => _evolutionPointsRequireOvipositorAfter = TimeSpan.FromMinutes(v), true);
        Subs.CVar(_config, RMCCVars.RMCEvolutionPointsAccumulateBeforeMinutes, v => _evolutionAccumulatePointsBefore = TimeSpan.FromMinutes(v), true);
    }

    private void OnXenoOpenDevolveAction(Entity<XenoDevolveComponent> xeno, ref XenoOpenDevolveActionEvent args)
    {
        if (args.Handled)
            return;

        if (!CanDevolvePopup(xeno))
            return;

        args.Handled = true;
        _ui.OpenUi(xeno.Owner, XenoDevolveUIKey.Key, xeno);
    }

    private void OnXenoEvolveMapInit(Entity<XenoEvolutionComponent> ent, ref MapInitEvent args)
    {
        _action.AddAction(ent, ref ent.Comp.Action, ent.Comp.ActionId);
    }

    private void OnXenoEvolveAction(Entity<XenoEvolutionComponent> xeno, ref XenoOpenEvolutionsActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _ui.OpenUi(xeno.Owner, XenoEvolutionUIKey.Key, xeno);

        var state = new XenoEvolveBuiState(LackingOvipositor());
        _ui.SetUiState(xeno.Owner, XenoEvolutionUIKey.Key, state);
    }

    private void OnXenoEvolveBui(Entity<XenoEvolutionComponent> xeno, ref XenoEvolveBuiMsg args)
    {
        var actor = args.Actor;
        _ui.CloseUi(xeno.Owner, XenoEvolutionUIKey.Key, actor);

        if (_net.IsClient)
            return;

        if (!CanEvolvePopup(xeno, args.Choice))
        {
            Log.Warning($"{ToPrettyString(actor)} sent an invalid evolution choice: {args.Choice}.");
            return;
        }

        if (!DamagedCheckPopup(xeno, false))
            return;

        var time = _timing.CurTime;
        if (_prototypes.TryIndex(args.Choice, out var choice) &&
            choice.HasComponent<XenoEvolutionGranterComponent>(_compFactory) &&
            TryComp(xeno, out XenoComponent? xenoComp) &&
            TryComp(xenoComp.Hive, out HiveComponent? hive) &&
            hive.LastQueenDeath is { } lastQueenDeath &&
            time < lastQueenDeath + hive.NewQueenCooldown)
        {
            var left = lastQueenDeath + hive.NewQueenCooldown - time;
            var msg = Loc.GetString("rmc-xeno-evolution-cant-evolve-recent-queen-death-minutes",
                ("minutes", left.Minutes),
                ("seconds", left.Seconds));
            if (left.Minutes == 0)
            {
                msg = Loc.GetString("rmc-xeno-evolution-cant-evolve-recent-queen-death-seconds",
                    ("seconds", left.Seconds));
            }

            _popup.PopupEntity(msg, xeno, xeno, PopupType.MediumCaution);
            return;
        }

        var ev = new XenoEvolutionDoAfterEvent(args.Choice);
        var doAfter = new DoAfterArgs(EntityManager, xeno, xeno.Comp.EvolutionDelay, ev, xeno);

        if (xeno.Comp.EvolutionDelay > TimeSpan.Zero)
            _popup.PopupClient(Loc.GetString("cm-xeno-evolution-start"), xeno, xeno);

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnXenoStrainBui(Entity<XenoEvolutionComponent> xeno, ref XenoStrainBuiMsg args)
    {
        var actor = args.Actor;
        _ui.CloseUi(xeno.Owner, XenoEvolutionUIKey.Key, actor);

        if (_net.IsClient)
            return;

        if (!xeno.Comp.Strains.Contains(args.Choice))
        {
            Log.Warning($"{ToPrettyString(actor)} sent an invalid strain choice: {args.Choice}.");
            return;
        }

        if (!ContainedCheckPopup(xeno))
            return;

        if (!DamagedCheckPopup(xeno, false))
            return;

        var newXeno = TransferXeno(xeno, args.Choice);
        var ev = new NewXenoEvolvedEvent(xeno);
        RaiseLocalEvent(newXeno, ref ev);

        _adminLog.Add(LogType.RMCEvolve, $"Xenonid {ToPrettyString(xeno)} chose strain {ToPrettyString(newXeno)}");

        Del(xeno.Owner);

        var afterEv = new AfterNewXenoEvolvedEvent();
        RaiseLocalEvent(newXeno, ref afterEv);
    }

    private void OnXenoDevolveBui(Entity<XenoDevolveComponent> xeno, ref XenoDevolveBuiMsg args)
    {
        _ui.CloseUi(xeno.Owner, XenoEvolutionUIKey.Key, xeno);

        if (!CanDevolvePopup(xeno))
            return;

        if (_net.IsClient ||
            !xeno.Comp.DevolvesTo.Contains(args.Choice))
        {
            return;
        }

        var newXeno = TransferXeno(xeno, args.Choice);
        var ev = new XenoDevolvedEvent(xeno);
        RaiseLocalEvent(newXeno, ref ev);

        _adminLog.Add(LogType.RMCDevolve, $"Xenonid {ToPrettyString(xeno)} devolved into {ToPrettyString(newXeno)}");

        Del(xeno.Owner);

        _popup.PopupEntity(Loc.GetString("rmc-xeno-evolution-devolve", ("xeno", newXeno)), newXeno, newXeno, PopupType.LargeCaution);
    }

    private void OnXenoEvolveDoAfter(Entity<XenoEvolutionComponent> xeno, ref XenoEvolutionDoAfterEvent args)
    {
        if (_net.IsClient ||
            args.Handled ||
            args.Cancelled ||
            !_mind.TryGetMind(xeno, out var mindId, out _) ||
            !CanEvolvePopup(xeno, args.Choice))
        {
            return;
        }

        args.Handled = true;

        var newXeno = TransferXeno(xeno, args.Choice);
        var ev = new NewXenoEvolvedEvent(xeno);
        RaiseLocalEvent(newXeno, ref ev);

        _adminLog.Add(LogType.RMCEvolve, $"Xenonid {ToPrettyString(xeno)} evolved into {ToPrettyString(newXeno)}");

        Del(xeno.Owner);

        _popup.PopupEntity(Loc.GetString("cm-xeno-evolution-end"), newXeno, newXeno);

        var afterEv = new AfterNewXenoEvolvedEvent();
        RaiseLocalEvent(newXeno, ref afterEv);
    }

    private void OnXenoEvolutionNewEvolved(Entity<XenoEvolutionComponent> xeno, ref NewXenoEvolvedEvent args)
    {
        TransferPoints((args.OldXeno, args.OldXeno), xeno, true);
    }

    private void OnXenoEvolutionDevolved(Entity<XenoEvolutionComponent> xeno, ref XenoDevolvedEvent args)
    {
        TransferPoints(args.OldXeno, (xeno, xeno), false);
    }

    private void TransferPoints(Entity<XenoEvolutionComponent?> old, Entity<XenoEvolutionComponent> xeno, bool subtract)
    {
        if (!Resolve(old, ref old.Comp, false))
            return;

        xeno.Comp.Points = subtract ? FixedPoint2.Max(0, old.Comp.Points - old.Comp.Max) : old.Comp.Points;

        Dirty(xeno);
    }

    private void OnNewlyEvolvedPreventCollide(Entity<XenoNewlyEvolvedComponent> ent, ref PreventCollideEvent args)
    {
        if (ent.Comp.StopCollide.Contains(args.OtherEntity))
            args.Cancelled = true;
    }

    private void OnGranterEvolved(Entity<XenoEvolutionGranterComponent> ent, ref NewXenoEvolvedEvent args)
    {
        _xenoAnnounce.AnnounceSameHive(ent.Owner, Loc.GetString("rmc-new-queen"));
    }

    private void OnOvipositorChanged(ref XenoOvipositorChangedEvent ev)
    {
        if (_net.IsClient)
            return;

        var xenos = EntityQueryEnumerator<ActorComponent, XenoEvolutionComponent>();
        var state = new XenoEvolveBuiState(LackingOvipositor());
        while (xenos.MoveNext(out var uid, out _, out _))
        {
            _ui.SetUiState(uid, XenoEvolutionUIKey.Key, state);
        }
    }

    private bool ContainedCheckPopup(EntityUid xeno, bool doPopup = true)
    {
        if (!_container.IsEntityInContainer(xeno))
            return true;

        if (doPopup)
            _popup.PopupEntity(Loc.GetString("rmc-xeno-evolution-failed-bad-location"), xeno, xeno, PopupType.MediumCaution);

        return false;
    }

    private bool DamagedCheckPopup(EntityUid xeno, bool predicted = true, bool doPopup = true)
    {
        if (!TryComp(xeno, out DamageableComponent? damageable) ||
            damageable.TotalDamage <= 1)
            return true;

        if (predicted)
            _popup.PopupClient(Loc.GetString("rmc-xeno-evolution-cant-evolve-damaged"), xeno, xeno, PopupType.MediumCaution);
        else
            _popup.PopupEntity(Loc.GetString("rmc-xeno-evolution-cant-evolve-damaged"), xeno, xeno, PopupType.MediumCaution);

        return false;
    }

    private bool CanEvolvePopup(Entity<XenoEvolutionComponent> xeno, EntProtoId newXeno, bool doPopup = true)
    {
        if (!xeno.Comp.EvolvesTo.Contains(newXeno) && !xeno.Comp.EvolvesToWithoutPoints.Contains(newXeno))
            return false;

        if (!_prototypes.TryIndex(newXeno, out var prototype))
            return true;

        if (!ContainedCheckPopup(xeno, doPopup))
            return false;

        // TODO RMC14 revive jelly when added should not bring back dead queens
        if (prototype.TryGetComponent(out XenoEvolutionCappedComponent? capped, _compFactory) &&
            HasLiving<XenoEvolutionCappedComponent>(capped.Max, e => e.Comp.Id == capped.Id))
        {
            if (doPopup)
                _popup.PopupEntity(Loc.GetString("cm-xeno-evolution-failed-already-have", ("prototype", prototype.Name)), xeno, xeno, PopupType.MediumCaution);

            return false;
        }

        // TODO RMC14 only allow evolving towards Queen if none is alive
        if (!xeno.Comp.CanEvolveWithoutGranter && !HasLiving<XenoEvolutionGranterComponent>(1))
        {
            if (doPopup)
            {
                _popup.PopupEntity(
                    Loc.GetString("cm-xeno-evolution-failed-hive-shaken"),
                    xeno,
                    xeno,
                    PopupType.MediumCaution
                );
            }

            return false;
        }

        prototype.TryGetComponent(out XenoComponent? newXenoComp, _compFactory);
        if (newXenoComp != null &&
            newXenoComp.UnlockAt > _gameTicker.RoundDuration())
        {
            if (doPopup)
            {
                _popup.PopupEntity(
                    Loc.GetString("cm-xeno-evolution-failed-cannot-support"),
                    xeno,
                    xeno,
                    PopupType.MediumCaution
                );
            }

            // return false;
        }

        if (newXenoComp != null &&
            !newXenoComp.BypassTierCount &&
            TryComp(xeno, out XenoComponent? oldXenoComp) &&
            oldXenoComp.Hive is { } oldHive &&
            _xenoHive.TryGetTierLimit(oldHive, newXenoComp.Tier, out var limit))
        {
            var existing = 0;
            var total = 0;
            var current = EntityQueryEnumerator<XenoComponent>();
            while (current.MoveNext(out var existingComp))
            {
                if (existingComp.Hive != oldHive || !existingComp.CountedInSlots)
                    continue;

                total++;

                if (existingComp.Tier < newXenoComp.Tier)
                    continue;

                existing++;
            }

            if (_xenoHive.TryGetFreeSlots(oldHive, newXeno, out var freeSlots))
                existing -= freeSlots;

            if (total != 0 && existing / (float) total >= limit)
            {
                if (doPopup)
                {
                    _popup.PopupEntity(
                        Loc.GetString("cm-xeno-evolution-failed-hive-full", ("tier", newXenoComp.Tier)),
                        xeno,
                        xeno,
                        PopupType.MediumCaution
                    );
                }

                return false;
            }
        }

        return true;
    }

    private bool CanEvolveAny(Entity<XenoEvolutionComponent> xeno)
    {
        if (xeno.Comp.Points >= xeno.Comp.Max && xeno.Comp.EvolvesTo.Count > 0)
            return true;

        foreach (var evolution in xeno.Comp.EvolvesToWithoutPoints)
        {
            if (CanEvolvePopup(xeno, evolution, false))
                return true;
        }

        return false;
    }

    private bool CanDevolvePopup(EntityUid xeno, bool predicted = true)
    {
        return DamagedCheckPopup(xeno, predicted);
    }

    // TODO RMC14 make this a property of the hive component
    // TODO RMC14 per-hive
    public int GetLiving<T>(Predicate<Entity<T>>? predicate = null) where T : IComponent
    {
        var total = 0;
        var query = EntityQueryEnumerator<T>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_mobStateQuery.TryComp(uid, out var mobState) &&
                _mobState.IsDead(uid, mobState))
            {
                continue;
            }

            if (predicate != null && !predicate((uid, comp)))
                continue;

            total++;
        }

        return total;
    }

    // TODO RMC14 make this a property of the hive component
    // TODO RMC14 per-hive
    public bool HasLiving<T>(int count, Predicate<Entity<T>>? predicate = null) where T : IComponent
    {
        if (count <= 0)
            return true;

        var total = 0;
        var query = EntityQueryEnumerator<T>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_mobStateQuery.TryComp(uid, out var mobState) &&
                _mobState.IsDead(uid, mobState))
            {
                continue;
            }

            if (predicate != null && !predicate((uid, comp)))
                continue;

            total++;

            if (total >= count)
                return true;
        }

        return false;
    }

    public void SetPoints(Entity<XenoEvolutionComponent> evolution, FixedPoint2 points)
    {
        evolution.Comp.Points = points;
        Dirty(evolution);
    }

    public bool NeedsOvipositor()
    {
        return _gameTicker.RoundDuration() > _evolutionPointsRequireOvipositorAfter;
    }

    public bool HasOvipositor()
    {
        return HasLiving<XenoEvolutionGranterComponent>(1, e => HasComp<XenoAttachedOvipositorComponent>(e));
    }

    public bool LackingOvipositor()
    {
        return NeedsOvipositor() && !HasOvipositor();
    }

    private EntityUid TransferXeno(EntityUid xeno, EntProtoId proto)
    {
        var coordinates = _transform.GetMoverCoordinates(xeno);
        var newXeno = Spawn(proto, coordinates);
        _xeno.SetSameHive(newXeno, xeno);

        if (_mind.TryGetMind(xeno, out var mindId, out _))
        {
            _mind.TransferTo(mindId, newXeno);
            _mind.UnVisit(mindId);
        }

        foreach (var held in _hands.EnumerateHeld(xeno))
        {
            _hands.TryDrop(xeno, held);
        }

        // TODO RMC14 this is a hack because climbing on a newly created entity does not work properly for the client
        var comp = EnsureComp<XenoNewlyEvolvedComponent>(newXeno);

        _doors.Clear();
        _entityLookup.GetEntitiesIntersecting(xeno, _doors);
        foreach (var id in _doors)
        {
            if (HasComp<DoorComponent>(id) || HasComp<AirlockComponent>(id))
                comp.StopCollide.Add(id);
        }

        return newXeno;
    }

    public override void Update(float frameTime)
    {
        var newly = EntityQueryEnumerator<XenoNewlyEvolvedComponent>();
        while (newly.MoveNext(out var uid, out var comp))
        {
            if (comp.TriedClimb)
            {
                _intersecting.Clear();
                _entityLookup.GetEntitiesIntersecting(uid, _intersecting);
                for (var i = comp.StopCollide.Count - 1; i >= 0; i--)
                {
                    var colliding = comp.StopCollide[i];
                    if (!_intersecting.Contains(colliding))
                        comp.StopCollide.RemoveAt(i);
                }

                if (comp.StopCollide.Count == 0)
                    RemCompDeferred<XenoNewlyEvolvedComponent>(uid);

                continue;
            }

            comp.TriedClimb = true;
            if (TryComp(uid, out ClimbingComponent? climbing))
            {
                _climbable.Clear();
                _entityLookup.GetEntitiesIntersecting(uid, _climbable);

                foreach (var intersecting in _climbable)
                {
                    if (HasComp<ClimbableComponent>(intersecting))
                    {
                        _climb.ForciblySetClimbing(uid, intersecting);
                        Dirty(uid, climbing);
                        break;
                    }
                }
            }
        }

        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var roundDuration = _gameTicker.RoundDuration();
        var needsOvipositor = NeedsOvipositor();
        var hasGranter = needsOvipositor
            ? HasOvipositor()
            : HasLiving<XenoEvolutionGranterComponent>(1);
        if (needsOvipositor)
        {
            var granters = EntityQueryEnumerator<XenoEvolutionGranterComponent>();
            while (granters.MoveNext(out var uid, out var granter))
            {
                if (granter.GotOvipositorPopup)
                    continue;

                granter.GotOvipositorPopup = true;
                Dirty(uid, granter);

                _popup.PopupEntity("It is time to settle down and let your children grow.",
                    uid,
                    uid,
                    PopupType.LargeCaution
                );

                _xenoHive.AnnounceNeedsOvipositorToSameHive(uid);
            }
        }

        var evolution = EntityQueryEnumerator<XenoEvolutionComponent>();
        while (evolution.MoveNext(out var uid, out var comp))
        {
            if (comp.Max == FixedPoint2.Zero)
                continue;

            if (time < comp.LastPointsAt + TimeSpan.FromSeconds(1))
                continue;

            comp.LastPointsAt = time;
            Dirty(uid, comp);

            if (!comp.GotPopup && CanEvolveAny((uid, comp)))
            {
                comp.GotPopup = true;
                Dirty(uid, comp);

                _popup.PopupEntity(Loc.GetString("cm-xeno-evolution-ready"), uid, uid, PopupType.Large);
                _audio.PlayEntity(comp.EvolutionReadySound, uid, uid);
                continue;
            }

            if (comp.Points < comp.Max || roundDuration < _evolutionAccumulatePointsBefore)
            {
                if (needsOvipositor && comp.RequiresGranter && !hasGranter)
                    continue;

                SetPoints((uid, comp), comp.Points + comp.PointsPerSecond);
            }
            else if (comp.Points > comp.Max)
            {
                SetPoints((uid, comp), FixedPoint2.Max(comp.Points - comp.PointsPerSecond, comp.Max));
            }
        }
    }
}
