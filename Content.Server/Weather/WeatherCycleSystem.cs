using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Weather;
using Content.Shared.CCVar;
using Content.Shared.Weather;
using Content.Shared.Coordinates;
using Robust.Shared.Map;
using Robust.Shared.Configuration;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Time
{
    public sealed partial class WeatherCycleSystem : EntitySystem
    {
        [Dependency] private readonly IConfigurationManager _configuration = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly IEntitySystemManager _entitySystem = default!;
        [Dependency] private readonly SharedWeatherSystem _weatherSystem = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        private TimeSystem? _timeSystem;
        private int _currentHour;
        private double _deltaTime;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<WeatherCycleComponent, ComponentInit>(OnMapInit);
            _timeSystem = _entitySystem.GetEntitySystem<TimeSystem>();
        }

        private void OnMapInit(EntityUid uid, WeatherCycleComponent comp, ComponentInit args)
        {
            comp.IsEnabled = _configuration.GetCVar(CCVars.CycleEnabled);
            SetNewWeather(comp);
            SetRandomCycleValues(comp);
            comp.IsEnabled = true;
        }

        private void SetRandomCycleValues(WeatherCycleComponent comp)
        {
            int previousStart = comp.WeatherCicleStart;

            comp.WeatherCicleDuration = _random.Next(comp.MinDuration, comp.MaxDuration);
            int newStartHour = _random.Next(comp.MinStartHour, comp.MaxStartHour);

            // Verifica se a nova hora de começo de um ciclo está em um intervalo de pelo menos 2 de diferença
            if (Math.Abs(newStartHour - previousStart) < 3)
            {
                comp.WeatherCicleStart = newStartHour >= comp.MaxStartHour ? newStartHour - 1 : newStartHour + 1;
            }
            else
            {
                comp.WeatherCicleStart = newStartHour;
            }
        }

        public override void Update(float frameTime)
        {
            if (_deltaTime >= 1.0)
            {
                _deltaTime = 0;
                _currentHour = _timeSystem!.GetStationTime().Hours;
                foreach (var comp in EntityQuery<WeatherCycleComponent>())
                {
                    if (comp.IsEnabled)
                    {
                        // Calcula a hora de término do ciclo
                        int endHour = (comp.WeatherCicleStart + comp.WeatherCicleDuration) % 24;
                        bool isWithinCycle;

                        // Verifica se está em ciclo
                        if (comp.WeatherCicleStart + comp.WeatherCicleDuration < 24)
                        {
                            isWithinCycle = _currentHour >= comp.WeatherCicleStart && _currentHour < endHour;
                        }
                        else
                        {
                            isWithinCycle = _currentHour >= comp.WeatherCicleStart || _currentHour < endHour;
                        }

                        // Verifica se o ciclo está ativo e se está em ciclo (IsCycleActive é para evitar um loop infinito de trocas sem parar)
                        if (!comp.IsCycleActive && isWithinCycle)
                        {
                            SetNewWeather(comp);
                            comp.IsCycleActive = true;
                        }
                        else if (comp.IsCycleActive && !isWithinCycle)
                        {
                            SetNewWeather(comp, true);
                            SetRandomCycleValues(comp);
                            comp.IsCycleActive = false;
                        }
                    }
                }
            }
            else
            {
                _deltaTime += frameTime;
            }
        }

        private void SetNewWeather(WeatherCycleComponent comp, bool? clean = null)
        {
            if (comp.WeatherIds.Count == 0)
                return;

            var curTime = _timing.CurTime;
            var maxTime = TimeSpan.MaxValue;

            var mapId = comp.Owner.ToCoordinates().GetMapId(_entityManager);

            if (!_mapManager.MapExists(mapId))
                return;

            if (clean == true)
            {
                _weatherSystem.SetWeather(mapId, null, null);
                return;
            }

            var climaId = _random.Pick(comp.WeatherIds);

            if (TryComp<WeatherComponent>(_mapManager.GetMapEntityId(mapId), out var weatherComp) &&
                weatherComp.Weather.TryGetValue(climaId, out var existing))
            {
                maxTime = curTime - existing.StartTime;
            }

            if (!_prototypeManager.TryIndex<WeatherPrototype>(climaId, out var clima))
                return;

            _weatherSystem.SetWeather(mapId, clima, null);
        }

    }
}
