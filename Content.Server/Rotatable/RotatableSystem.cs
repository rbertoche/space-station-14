using Content.Server.Popups;
using Content.Shared.Popups;
using Content.Shared.Rotatable;
using Content.Shared.Verbs;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;
using Robust.Shared.Map;
using Content.Shared.Input;
using Content.Shared.ActionBlocker;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;

namespace Content.Server.Rotatable
{
    /// <summary>
    ///     Handles verbs for the <see cref="RotatableComponent"/> and <see cref="FlippableComponent"/> components.
    /// </summary>
    public sealed class RotatableSystem : EntitySystem
    {
        [Dependency] private readonly PopupSystem _popup = default!;
        [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;

        public override void Initialize()
        {
            SubscribeLocalEvent<FlippableComponent, GetVerbsEvent<Verb>>(AddFlipVerb);
            SubscribeLocalEvent<RotatableComponent, GetVerbsEvent<Verb>>(AddRotateVerbs);

            CommandBinds.Builder
                .Bind(ContentKeyFunctions.RotateObjectClockwise, new PointerInputCmdHandler(HandleRotateObjectClockwise))
                .Bind(ContentKeyFunctions.RotateObjectCounterclockwise, new PointerInputCmdHandler(HandleRotateObjectCounterclockwise))
                .Bind(ContentKeyFunctions.FlipObject, new PointerInputCmdHandler(HandleFlipObject))
                .Register<RotatableSystem>();
        }

        private void AddFlipVerb(EntityUid uid, FlippableComponent component, GetVerbsEvent<Verb> args)
        {
            if (!args.CanAccess || !args.CanInteract)
                return;

            Verb verb = new()
            {
                Act = () => TryFlip(uid, component, args.User),
                Text = Loc.GetString("flippable-verb-get-data-text"),
                Category = VerbCategory.Rotate,
                Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/flip.svg.192dpi.png")),
                Priority = -3, // show flip last
                DoContactInteraction = true
            };
            args.Verbs.Add(verb);
        }

        private void AddRotateVerbs(EntityUid uid, RotatableComponent component, GetVerbsEvent<Verb> args)
        {
            if (!args.CanAccess
                || !args.CanInteract
                || Transform(uid).NoLocalRotation)
                return;

            if (!component.RotateWhileAnchored &&
                EntityManager.TryGetComponent(uid, out PhysicsComponent? physics) &&
                physics.BodyType == BodyType.Static)
                return;

            Verb resetRotation = new ()
            {
                DoContactInteraction = true,
                Act = () => EntityManager.GetComponent<TransformComponent>(uid).LocalRotation = Angle.Zero,
                Category = VerbCategory.Rotate,
                Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/refresh.svg.192dpi.png")),
                Text = "Reset",
                Priority = -2,
                CloseMenu = false,
            };
            args.Verbs.Add(resetRotation);

            Verb rotateCW = new()
            {
                Act = () => EntityManager.GetComponent<TransformComponent>(uid).LocalRotation -= component.Increment,
                Category = VerbCategory.Rotate,
                Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/rotate_cw.svg.192dpi.png")),
                Priority = -1,
                CloseMenu = false,
            };
            args.Verbs.Add(rotateCW);

            Verb rotateCCW = new()
            {
                Act = () => EntityManager.GetComponent<TransformComponent>(uid).LocalRotation += component.Increment,
                Category = VerbCategory.Rotate,
                Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/rotate_ccw.svg.192dpi.png")),
                Priority = 0,
                CloseMenu = false,
            };
            args.Verbs.Add(rotateCCW);
        }

        public bool HandleRotateObjectClockwise(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid entity)
        {
            if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player))
                return false;

            if (!TryComp<RotatableComponent>(entity, out var rotatableComp))
                return false;

            if (!_actionBlocker.CanInteract(player, entity))
                return false;

            if (!rotatableComp.RotateWhileAnchored && EntityManager.TryGetComponent(entity, out PhysicsComponent? physics) &&
                physics.BodyType == BodyType.Static)
            {
                _popup.PopupEntity(Loc.GetString("rotatable-component-try-rotate-stuck"), entity, player);
                return false;
            }

            Transform(entity).LocalRotation -= rotatableComp.Increment;
            return true;
        }

        public bool HandleRotateObjectCounterclockwise(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid entity)
        {
            if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player))
                return false;

            if (!TryComp<RotatableComponent>(entity, out var rotatableComp))
                return false;

            if (!_actionBlocker.CanInteract(player, entity))
                return false;

            if (!rotatableComp.RotateWhileAnchored && EntityManager.TryGetComponent(entity, out PhysicsComponent? physics) &&
                physics.BodyType == BodyType.Static)
            {
                _popup.PopupEntity(Loc.GetString("rotatable-component-try-rotate-stuck"), entity, player);
                return false;
            }

            Transform(entity).LocalRotation += rotatableComp.Increment;
            return true;
        }

        public bool HandleFlipObject(ICommonSession? playerSession, EntityCoordinates coordinates, EntityUid entity)
        {
            if (playerSession?.AttachedEntity is not { Valid: true } player || !Exists(player))
                return false;

            if (!TryComp<FlippableComponent>(entity, out var flippableComp))
                return false;

            if (!_actionBlocker.CanInteract(player, entity))
                return false;

            if (EntityManager.TryGetComponent(entity, out PhysicsComponent? physics) && physics.BodyType == BodyType.Static)
            {
                _popup.PopupEntity(Loc.GetString("flippable-component-try-flip-is-stuck"), entity, player);
                return false;
            }

            TryFlip(entity, flippableComp, player);
            return true;
        }

        public void TryFlip(EntityUid uid, FlippableComponent component, EntityUid user)
        {
            var oldTransform = EntityManager.GetComponent<TransformComponent>(uid);
            var entity = EntityManager.SpawnEntity(component.MirrorEntity, oldTransform.Coordinates);
            var newTransform = EntityManager.GetComponent<TransformComponent>(entity);
            newTransform.LocalRotation = oldTransform.LocalRotation;
            newTransform.Anchored = false;
            EntityManager.DeleteEntity(uid);
        }
    }
}
