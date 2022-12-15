using Content.Shared.Actions.ActionTypes;

namespace Content.Shared.SimpleStation14.Abilities.Psionics
{
    [RegisterComponent]
    public sealed class AITelegnosisPowerComponent : Component
    {
        [DataField("prototype")]
        public string Prototype = "AIeye";
        public InstantAction? TelegnosisPowerAction = null;
    }
}
