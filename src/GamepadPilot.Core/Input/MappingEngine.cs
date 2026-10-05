using GamepadPilot.Core.Domain;
using GamepadPilot.Core.Services;

namespace GamepadPilot.Core.Input;

public sealed class MappingEngine
{
    private const int ComboGraceMs = 45;
    private const double VolumeStepsPerSecond = 10d;
    private readonly IActionDispatcher _dispatcher;
    private readonly object _gate = new();
    private readonly Dictionary<string, Runtime> _runtimes = new(StringComparer.Ordinal);
    private readonly Dictionary<GamepadControl, AnalogVector> _smoothedSticks = [];
    private AppConfiguration _configuration;
    private ProfileDefinition? _profile;
    private GamepadSnapshot _snapshot;
    private bool _enabled = true;
    private bool _suspended;
    private bool _armed;
    private DateTimeOffset _lastTick;

    public MappingEngine(AppConfiguration configuration, IActionDispatcher dispatcher)
    {
        _configuration = configuration;
        _dispatcher = dispatcher;
        _profile = configuration.FindProfile(configuration.ActiveProfileId) ?? configuration.Profiles.FirstOrDefault();
        _snapshot = GamepadSnapshot.Disconnected(DateTimeOffset.Now);
        _lastTick = DateTimeOffset.Now;
    }

    public bool IsEnabled
    {
        get { lock (_gate) return _enabled; }
    }

    public bool IsSuspended
    {
        get { lock (_gate) return _suspended; }
    }

    public GamepadSnapshot Snapshot
    {
        get { lock (_gate) return _snapshot; }
    }

    public void SetConfiguration(AppConfiguration configuration)
    {
        lock (_gate)
        {
            _configuration = configuration;
            _profile = configuration.FindProfile(configuration.ActiveProfileId) ?? configuration.Profiles.FirstOrDefault();
            ResetAndRelease();
        }
    }

    public void SetProfile(Guid profileId)
    {
        lock (_gate)
        {
            if (_profile?.Id == profileId)
            {
                return;
            }

            _profile = _configuration.FindProfile(profileId) ?? _configuration.Profiles.FirstOrDefault();
            _configuration.ActiveProfileId = _profile?.Id ?? Guid.Empty;
            ResetAndRelease();
        }
    }

    public void SetEnabled(bool enabled)
    {
        lock (_gate)
        {
            if (_enabled == enabled)
            {
                return;
            }

            _enabled = enabled;
            _armed = false;
            ResetAndRelease();
        }
    }

    public void SetSuspended(bool suspended)
    {
        lock (_gate)
        {
            if (_suspended == suspended)
            {
                return;
            }

            _suspended = suspended;
            _armed = false;
            ResetAndRelease();
        }
    }

    public void Process(GamepadSnapshot snapshot)
    {
        lock (_gate)
        {
            var now = snapshot.Timestamp;
            var deltaSeconds = Math.Clamp((now - _lastTick).TotalSeconds, 0d, 0.05d);
            _lastTick = now;
            _snapshot = snapshot;

            if (!snapshot.IsConnected || !_enabled || _suspended)
            {
                ResetAndRelease();
                return;
            }

            if (!_armed)
            {
                UpdateRuntimeBaseline(snapshot);
                if (HasAnyButtonDown(snapshot))
                {
                    return;
                }

                _armed = true;
            }

            var profile = _profile;
            if (profile is null)
            {
                return;
            }

            var comboStates = EvaluateCombos(profile, snapshot, now);
            var suppressedSources = comboStates
                .Where(x => x.Value)
                .SelectMany(x => x.Key.Buttons)
                .ToHashSet();

            EvaluateSticks(profile, snapshot, deltaSeconds);
            EvaluateButtons(profile, snapshot, now, suppressedSources, comboStates);
        }
    }

    private Dictionary<ComboBinding, bool> EvaluateCombos(ProfileDefinition profile, GamepadSnapshot snapshot, DateTimeOffset now)
    {
        var result = new Dictionary<ComboBinding, bool>();
        foreach (var combo in profile.Combos.Where(x => x.Enabled).OrderByDescending(x => x.Buttons.Count))
        {
            var down = combo.Buttons.Count > 0 && combo.Buttons.All(snapshot.IsDown);
            result[combo] = down;
        }
        return result;
    }

    private void EvaluateSticks(ProfileDefinition profile, GamepadSnapshot snapshot, double deltaSeconds)
    {
        foreach (var binding in profile.Bindings.Where(x => x.Enabled && x.Trigger == TriggerMode.Continuous))
        {
            if (binding.Source is GamepadControl.LeftTrigger or GamepadControl.RightTrigger)
            {
                if (binding.Action.Type != ActionType.Volume) continue;
                var rawTrigger = binding.Source == GamepadControl.LeftTrigger ? snapshot.LeftTrigger : snapshot.RightTrigger;
                var triggerValue = rawTrigger / 255d;
                var normalized = Math.Clamp((triggerValue - 0.05d) / 0.95d, 0d, 1d);
                normalized = Math.Pow(normalized, Math.Clamp(binding.Action.Curve, 0.5d, 3d));
                var direction = binding.Action.VolumeDirection == VolumeDirection.Up ? 1d : -1d;
                var volumeDelta = direction * normalized * VolumeStepsPerSecond * deltaSeconds;
                if (volumeDelta != 0d) _dispatcher.Continuous(binding.Action, 0d, volumeDelta, deltaSeconds);
                continue;
            }

            if (binding.Source is not (GamepadControl.LeftStick or GamepadControl.RightStick)) continue;
            var settings = binding.Source == GamepadControl.LeftStick ? profile.LeftStick : profile.RightStick;
            if (binding.Action.Curve > 0d)
            {
                settings = new StickSettings
                {
                    DeadZone = settings.DeadZone,
                    Sensitivity = settings.Sensitivity,
                    Curve = binding.Action.Curve,
                    ScrollRate = settings.ScrollRate,
                    Smoothing = settings.Smoothing,
                    InvertY = settings.InvertY
                };
            }
            var rawX = binding.Source == GamepadControl.LeftStick ? snapshot.LeftThumbX : snapshot.RightThumbX;
            var rawY = binding.Source == GamepadControl.LeftStick ? snapshot.LeftThumbY : snapshot.RightThumbY;
            _smoothedSticks.TryGetValue(binding.Source, out var previous);
            var vector = AnalogMath.Apply(rawX, rawY, settings, previous, deltaSeconds);
            _smoothedSticks[binding.Source] = vector;
            if (vector.Magnitude <= 0d)
            {
                continue;
            }

            var rate = binding.Action.Type == ActionType.MouseWheel ? settings.ScrollRate : settings.Sensitivity;
            var scale = Math.Clamp(binding.Scale, 0.1d, 5d) * Math.Clamp(binding.Action.Scale, 0.1d, 5d);
            var x = vector.X * rate * scale * deltaSeconds;
            var directionY = settings.InvertY ? -vector.Y : vector.Y;
            var y = directionY * rate * scale * deltaSeconds;
            _dispatcher.Continuous(binding.Action, x, y, deltaSeconds);
        }
    }

    private void EvaluateButtons(ProfileDefinition profile, GamepadSnapshot snapshot, DateTimeOffset now, HashSet<GamepadControl> suppressedSources, IReadOnlyDictionary<ComboBinding, bool> comboStates)
    {
        var grouped = profile.Bindings
            .Where(x => x.Enabled && x.Trigger != TriggerMode.Continuous)
            .GroupBy(x => x.Source);

        foreach (var group in grouped)
        {
            var refs = group.Select(x => new BindingRef(x.Trigger, x.Action)).ToArray();
            var delay = _configuration.Profiles.Any(p => p.Combos.Any(c => c.Enabled && c.Buttons.Contains(group.Key))) ? ComboGraceMs : 0;
            EvaluateRuntime($"control:{group.Key}", snapshot.IsDown(group.Key), refs, now, suppressedSources.Contains(group.Key), delay);
        }

        foreach (var pair in comboStates)
        {
            EvaluateRuntime($"combo:{pair.Key.Id}", pair.Value, [new BindingRef(pair.Key.Trigger, pair.Key.Action)], now, false, 0);
        }
    }

    private void EvaluateRuntime(
        string key,
        bool down,
        IReadOnlyList<BindingRef> bindings,
        DateTimeOffset now,
        bool suppressed,
        int activationDelayMs)
    {
        if (!_runtimes.TryGetValue(key, out var runtime))
        {
            runtime = new Runtime { WasDown = down };
            _runtimes[key] = runtime;
        }

        BindingRef? hold = bindings.Cast<BindingRef?>().FirstOrDefault(x => x!.Value.Trigger == TriggerMode.Hold);
        BindingRef? press = bindings.Cast<BindingRef?>().FirstOrDefault(x => x!.Value.Trigger == TriggerMode.Press);
        BindingRef? longPress = bindings.Cast<BindingRef?>().FirstOrDefault(x => x!.Value.Trigger == TriggerMode.LongPress);
        BindingRef? doublePress = bindings.Cast<BindingRef?>().FirstOrDefault(x => x!.Value.Trigger == TriggerMode.DoublePress);

        if (!down)
        {
            if (runtime.WasDown)
            {
                if (runtime.HoldActive && hold is not null)
                {
                    _dispatcher.Release(hold.Value.Action);
                    runtime.HoldActive = false;
                }

                if (doublePress is not null && press is not null && !runtime.LongFired && !runtime.DoubleConsumed && !suppressed)
                {
                    runtime.PendingSingleAt = now.AddMilliseconds(_configuration.Settings.DoublePressWindowMs);
                }

                runtime.LastReleaseAt = now;
            }

            if (press is not null && runtime.PendingSingleAt is { } pending && pending <= now && !suppressed)
            {
                _dispatcher.Execute(press.Value.Action);
                runtime.PendingSingleAt = null;
            }

            runtime.WasDown = false;
            runtime.PressFired = false;
            runtime.LongFired = false;
            runtime.DoubleConsumed = false;
            runtime.PendingPressAt = null;
            return;
        }

        if (!runtime.WasDown)
        {
            runtime.DownAt = now;
            runtime.WasDown = true;
            runtime.PressFired = false;
            runtime.LongFired = false;
            runtime.HoldActive = false;
            runtime.PendingSingleAt = null;

            var doubleWindow = TimeSpan.FromMilliseconds(_configuration.Settings.DoublePressWindowMs);
            if (doublePress is not null && now - runtime.LastReleaseAt <= doubleWindow)
            {
                _dispatcher.Execute(doublePress.Value.Action);
                runtime.DoubleConsumed = true;
                runtime.PendingPressAt = null;
                return;
            }

            runtime.DoubleConsumed = false;
            runtime.PendingPressAt = now.AddMilliseconds(activationDelayMs);
        }

        if (suppressed)
        {
            if (runtime.HoldActive && hold is not null)
            {
                _dispatcher.Release(hold.Value.Action);
                runtime.HoldActive = false;
            }

            runtime.PendingSingleAt = null;
            runtime.PendingPressAt = null;
            runtime.PressFired = true;
            return;
        }

        if (runtime.DoubleConsumed || runtime.PendingPressAt is { } pendingActivation && pendingActivation > now)
        {
            return;
        }

        var longThreshold = TimeSpan.FromMilliseconds(_configuration.Settings.LongPressWindowMs);
        if (longPress is not null && !runtime.LongFired && now - runtime.DownAt >= longThreshold)
        {
            _dispatcher.Execute(longPress.Value.Action);
            runtime.LongFired = true;
            runtime.PressFired = true;
        }

        if (hold is not null && !runtime.HoldActive)
        {
            _dispatcher.Press(hold.Value.Action);
            runtime.HoldActive = true;
        }

        if (press is not null && !runtime.PressFired && longPress is null && doublePress is null)
        {
            _dispatcher.Execute(press.Value.Action);
            runtime.PressFired = true;
        }
    }

    private void UpdateRuntimeBaseline(GamepadSnapshot snapshot)
    {
        foreach (var control in ControlCatalog.ButtonControls)
        {
            var runtime = GetRuntime($"control:{control}");
            runtime.WasDown = snapshot.IsDown(control);
            runtime.PendingPressAt = null;
            runtime.PendingSingleAt = null;
        }

        foreach (var combo in _configuration.Profiles.SelectMany(x => x.Combos))
        {
            var runtime = GetRuntime($"combo:{combo.Id}");
            runtime.WasDown = combo.Buttons.Count > 0 && combo.Buttons.All(snapshot.IsDown);
        }
    }

    private Runtime GetRuntime(string key)
    {
        if (!_runtimes.TryGetValue(key, out var runtime))
        {
            runtime = new Runtime();
            _runtimes[key] = runtime;
        }
        return runtime;
    }

    private static bool HasAnyButtonDown(GamepadSnapshot snapshot) =>
        ControlCatalog.ButtonControls.Any(snapshot.IsDown);

    private void ResetAndRelease()
    {
        _runtimes.Clear();
        _smoothedSticks.Clear();
        _dispatcher.ReleaseAll();
    }

    private sealed class Runtime
    {
        public bool WasDown;
        public DateTimeOffset DownAt;
        public DateTimeOffset LastReleaseAt = DateTimeOffset.MinValue;
        public DateTimeOffset? PendingPressAt;
        public DateTimeOffset? PendingSingleAt;
        public bool PressFired;
        public bool LongFired;
        public bool HoldActive;
        public bool DoubleConsumed;
    }

    private readonly record struct BindingRef(TriggerMode Trigger, ActionDefinition Action);
}

