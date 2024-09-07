using Content.Shared.Weather;
using System.ComponentModel.DataAnnotations;
using Robust.Shared.Prototypes;

namespace Content.Server.Weather
{
    [RegisterComponent]
    public sealed partial class WeatherCycleComponent : Component
    {
        [ViewVariables(VVAccess.ReadWrite), DataField("isEnabled")]
        public bool IsEnabled = true;
        [ViewVariables(VVAccess.ReadOnly), DataField("weatherCicleDuration")]
        public int WeatherCicleDuration = 9;
        [ViewVariables(VVAccess.ReadWrite), DataField("weatherCicleStart")]
        public int WeatherCicleStart = 19;
        [ViewVariables(VVAccess.ReadWrite), DataField("minDuration")]
        public int MinDuration = 3;
        [ViewVariables(VVAccess.ReadWrite), DataField("maxDuration")]
        public int MaxDuration = 8;
        [ViewVariables(VVAccess.ReadWrite), DataField("minStartHour")]
        public int MinStartHour = 1;
        [ViewVariables(VVAccess.ReadWrite), DataField("maxStartHour")]
        public int MaxStartHour = 23;
        [ViewVariables(VVAccess.ReadWrite), DataField("weatherIds")]
        public List<string> WeatherIds { get; set; } = new();
        public bool IsCycleActive { get; set; } = false;
    }
}
