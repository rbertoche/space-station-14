using Content.Shared.Hands.Components;
using Robust.Shared.Utility;

namespace Content.Shared.DoAfter;

public abstract partial class SharedDoAfterSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = GameTiming.CurTime;
        var xformQuery = GetEntityQuery<TransformComponent>();
        var handsQuery = GetEntityQuery<SharedHandsComponent>();

        var enumerator = EntityQueryEnumerator<ActiveDoAfterComponent, DoAfterComponent>();
        while (enumerator.MoveNext(out var uid, out var active, out var comp))
        {
            Update(uid, active, comp, time, xformQuery, handsQuery);
        }
    }

    protected void Update(
        EntityUid uid,
        ActiveDoAfterComponent active,
        DoAfterComponent comp,
        TimeSpan time,
        EntityQuery<TransformComponent> xformQuery,
        EntityQuery<SharedHandsComponent> handsQuery)
    {
        var dirty = false;

        foreach (var doAfter in comp.DoAfters.Values)
        {
            if (doAfter.CancelledTime != null)
            {
                if (time - doAfter.CancelledTime.Value > ExcessTime)
                {
                    comp.DoAfters.Remove(doAfter.Index);
                    dirty = true;
                }
                continue;
            }

            if (doAfter.Completed)
            {
                if (time - doAfter.StartTime > doAfter.Args.Delay + ExcessTime)
                {
                    comp.DoAfters.Remove(doAfter.Index);
                    dirty = true;
                }
                continue;
            }

            if (ShouldCancel(doAfter, xformQuery, handsQuery))
            {
                InternalCancel(doAfter, comp);
                dirty = true;
                continue;
            }

            if (time - doAfter.StartTime >= doAfter.Args.Delay)
            {
                TryComplete(doAfter, comp);
                dirty = true;
            }
        }

        if (dirty)
            Dirty(comp);

        if (comp.DoAfters.Count == 0)
            RemCompDeferred(uid, active);
    }

    private bool TryAttemptEvent(DoAfter doAfter)
    {
        var args = doAfter.Args;

        if (args.ExtraCheck?.Invoke() == false)
            return false;

        if (args.AttemptEvent == null)
            return true;

        var ev = args.AttemptEvent;
        ev.Uncancel();
        ev.DoAfter = doAfter;
        if (args.EventTarget != null)
            RaiseLocalEvent(args.EventTarget.Value, (object)ev, args.Broadcast);
        else
            RaiseLocalEvent((object)ev);

        return !args.AttemptEvent.Cancelled;
    }

    private void TryComplete(DoAfter doAfter, DoAfterComponent component)
    {
        if (doAfter.Cancelled || doAfter.Completed)
            return;

        // Perform final check (if required)
        if (!doAfter.Args.AttemptEveryTick && !TryAttemptEvent(doAfter))
        {
            InternalCancel(doAfter, component);
            return;
        }

        doAfter.Completed = true;
        RaiseDoAfterEvents(doAfter, component);
    }

    private bool ShouldCancel(DoAfter doAfter,
        EntityQuery<TransformComponent> xformQuery,
        EntityQuery<SharedHandsComponent> handsQuery)
    {
        var args = doAfter.Args;

        //re-using xformQuery for Exists() checks.
        if (args.Used is { } used && !xformQuery.HasComponent(used))
            return true;

        if (args.EventTarget is { Valid: true} eventTarget && !xformQuery.HasComponent(eventTarget))
            return true;

        if (!xformQuery.TryGetComponent(args.User, out var userXform))
            return true;

        TransformComponent? targetXform = null;
        if (args.Target is { } target && !xformQuery.TryGetComponent(target, out targetXform))
            return true;

        TransformComponent? usedXform = null;
        if (args.Used is { } @using && !xformQuery.TryGetComponent(@using, out usedXform))
            return true;

        // TODO: Handle Inertia in space
        // TODO: Re-use existing xform query for these calculations.
        if (args.BreakOnUserMove && !userXform.Coordinates
                .InRange(EntityManager, _transform, doAfter.UserPosition, args.MovementThreshold))
            return true;

        if (args.BreakOnTargetMove)
        {
            DebugTools.Assert(targetXform != null, "Break on move is true, but no target specified?");
            if (targetXform != null && !targetXform.Coordinates.InRange(EntityManager, _transform, doAfter.TargetPosition, args.MovementThreshold))
                return true;
        }

        if (args.AttemptEveryTick && !TryAttemptEvent(doAfter))
            return true;

        if (args.NeedHand)
        {
            if (!handsQuery.TryGetComponent(args.User, out var hands)
                || hands.ActiveHand?.Name != doAfter.ActiveHand
                || hands.ActiveHandEntity != doAfter.ActiveItem)
                return true;
        }

        if (args.RequireCanInteract && !_actionBlocker.CanInteract(args.User, args.Target))
            return true;

        if (args.DistanceThreshold != null)
        {
            if (targetXform != null
                && !args.User.Equals(args.Target)
                && !userXform.Coordinates.InRange(EntityManager, _transform, targetXform.Coordinates, args.DistanceThreshold.Value))
            {
                return true;
            }

            if (usedXform != null
                && !userXform.Coordinates.InRange(EntityManager, _transform, usedXform.Coordinates, args.DistanceThreshold.Value))
            {
                return true;
            }
        }

        return false;
    }
}
