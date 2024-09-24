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
        [ViewVariables(VVAccess.ReadOnly), DataField("nextWeatherCyclePauseEnd")]
        public TimeSpan NextWeatherCyclePauseEnd { get; set; }
        [ViewVariables(VVAccess.ReadOnly), DataField("PauseDuration")]
        public TimeSpan PauseDuration { get; set; }
        [ViewVariables(VVAccess.ReadOnly), DataField("weatherCicleDuration")]
        public TimeSpan WeatherCicleDuration = TimeSpan.FromMinutes(20);
        [ViewVariables(VVAccess.ReadWrite), DataField("NextWeatherCycleStart")]
        public TimeSpan NextWeatherCycleStart;
        [ViewVariables(VVAccess.ReadWrite), DataField("minDuration")]
        public int MinDuration = 1;
        [ViewVariables(VVAccess.ReadWrite), DataField("maxDuration")]
        public int MaxDuration = 5;
        public List<string> WeatherIds { get; set; } = new();
        public bool IsCycleActive { get; set; } = false;
    }
}
