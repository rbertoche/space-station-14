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
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly IEntitySystemManager _entitySystem = default!;
        [Dependency] private readonly SharedWeatherSystem _weatherSystem = default!;
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
            comp.WeatherCicleDuration = TimeSpan.FromMinutes(_random.Next(comp.MinDuration, comp.MaxDuration));
            // Define o tempo de início do próximo ciclo
            comp.NextWeatherCycleStart = _timing.CurTime + comp.WeatherCicleDuration;
            // Define o intervalo de pausa aleatório entre 5 a 10 minutos
            comp.PauseDuration = TimeSpan.FromMinutes(_random.Next(5, 10));
            comp.NextWeatherCyclePauseEnd = comp.NextWeatherCycleStart + comp.PauseDuration;
        }


        public override void Update(float frameTime)
        {
            if (_deltaTime >= 1.0)
            {
                _deltaTime = 0;

                foreach (var comp in EntityQuery<WeatherCycleComponent>())
                {
                    if (_timing.CurTime >= comp.NextWeatherCycleStart && comp.IsCycleActive)
                    {
                        // Termina o ciclo de clima
                        SetNewWeather(comp, true);
                        comp.IsCycleActive = false;
                        // Define os valores do próximo ciclo
                        SetRandomCycleValues(comp);
                    }
                    else if (_timing.CurTime >= comp.NextWeatherCyclePauseEnd && !comp.IsCycleActive)
                    {
                        // Inicia o novo ciclo de clima após a pausa
                        SetNewWeather(comp);
                        comp.IsCycleActive = true;
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

            var mapId = comp.Owner.ToCoordinates().GetMapId(_entityManager);

            if (!_mapManager.MapExists(mapId))
                return;

            if (clean == true)
            {
                _weatherSystem.SetWeather(mapId, null, null);
                return;
            }

            var climaId = _random.Pick(comp.WeatherIds);

            if (!_prototypeManager.TryIndex<WeatherPrototype>(climaId, out var clima))
                return;

            _weatherSystem.SetWeather(mapId, clima, null);
        }

    }
}
