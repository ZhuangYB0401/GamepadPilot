using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using System.Windows.Threading;
using GamepadPilot.App.Platform;
using GamepadPilot.Core.Configuration;
using GamepadPilot.Core.Domain;
using GamepadPilot.Core.Input;
using GamepadPilot.Core.Services;
using GamepadPilot.Core.Typing;

namespace GamepadPilot.App;

public sealed class AppController : INotifyPropertyChanged, IDisposable
{
    private readonly IProfileStore _store;
    private readonly FileLogger _logger;
    private readonly XInputGamepadSource _gamepadSource;
    private readonly WindowsInputSink _inputSink;
    private readonly InputDispatcher _inputDispatcher;
    private readonly MappingEngine _engine;
    private readonly ProfileResolver _profileResolver = new();
    private readonly IForegroundAppService _foregroundService;
    private readonly IFullscreenDetector _fullscreenDetector;
    private readonly GlobalHotkeyService _hotkeys;
    private readonly WindowsMacroRecorder _macroRecorder;
    private readonly TypingWheelController _typingWheel;
    private readonly FullscreenEscapeOverride _fullscreenEscapeOverride = new();
    private readonly TrayIconService _tray;
    private readonly DispatcherTimer _statusTimer;
    private Dispatcher? _uiDispatcher;
    private ForegroundAppInfo? _foreground;
    private FullscreenState _fullscreen = new(false, string.Empty);
    private bool _manualPause;
    private bool _uiInputCaptureActive;
    private bool _recordingWasSuspended;
    private DateTimeOffset _lastUiUpdate;
    private string _statusText = "正在启动…";
    private string _controllerText = "未连接手柄";
    private MacroDefinition? _recordingMacro;
    private TypingWheelWindow? _typingWheelWindow;

    public AppController(IProfileStore? store = null)
    {
        _store = store ?? new JsonProfileStore();
        _logger = new FileLogger();
        Configuration = _store.Load();
        _gamepadSource = new XInputGamepadSource();
        _inputSink = new WindowsInputSink();
        _inputDispatcher = new InputDispatcher(_inputSink);
        _engine = new MappingEngine(Configuration, _inputDispatcher);
        _foregroundService = new WindowsForegroundService();
        _fullscreenDetector = new WindowsFullscreenDetector();
        _hotkeys = new GlobalHotkeyService();
        _macroRecorder = new WindowsMacroRecorder();
        _typingWheel = new TypingWheelController(_inputSink);
        _tray = new TrayIconService();
        _statusTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
        IsAdministrator = CheckAdministrator();
        WireEvents();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? StateChanged;
    public event EventHandler? ConfigurationChanged;
    public event EventHandler? OpenRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler<MacroStep>? MacroStepRecorded;

    public AppConfiguration Configuration { get; private set; }
    public GamepadSnapshot LastSnapshot { get; private set; } = GamepadSnapshot.Disconnected(DateTimeOffset.Now);
    public bool IsEnabled => _engine.IsEnabled;
    public bool IsSuspended => _engine.IsSuspended;
    public bool IsManuallyPaused => _manualPause;
    public bool IsRecording => _macroRecorder.IsRecording;
    public bool IsAdministrator { get; }
    public string ConfigPath => _store.ConfigPath;
    public string StatusText { get => _statusText; private set => SetField(ref _statusText, value); }
    public string ControllerText { get => _controllerText; private set => SetField(ref _controllerText, value); }
    public bool IsFullscreen => _fullscreen.IsFullscreen;
    public string FullscreenReason => _fullscreen.Reason;

    public void Initialize(Window window)
    {
        _uiDispatcher = window.Dispatcher;
        _hotkeys.Attach(window);
        _hotkeys.RegisterAll(Configuration.Settings);
        _inputDispatcher.SetMacros(Configuration.Macros);
        _typingWheelWindow = new TypingWheelWindow();
        _typingWheel.StateChanged += OnTypingWheelStateChanged;
        _gamepadSource.Start();
        _statusTimer.Start();
        UpdateStatus();
        _logger.Info("GamepadPilot started.");
    }

    public void SetUiInputCaptureActive(bool active)
    {
        _uiInputCaptureActive = active;
        UpdateStatus();
    }

    public void SetEnabled(bool enabled)
    {
        _engine.SetEnabled(enabled);
        if (!enabled)
        {
            _typingWheel.SetActive(false);
            _inputSink.ReleaseAll();
        }
        UpdateStatus();
    }

    public void ToggleEnabled() => SetEnabled(!IsEnabled);

    public void ToggleManualPause()
    {
        _manualPause = !_manualPause;
        UpdateStatus();
    }

    public void SelectProfile(Guid profileId, bool manual)
    {
        if (_store is not null && Configuration.FindProfile(profileId) is null)
        {
            return;
        }

        if (manual)
        {
            _profileResolver.SelectManual(Configuration, profileId);
        }

        _engine.SetProfile(profileId);
        Configuration.ActiveProfileId = profileId;
        UpdateStatus();
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    public void CommitConfiguration(bool registerHotkeys = true)
    {
        ConfigurationValidator.Normalize(Configuration);
        _inputDispatcher.SetMacros(Configuration.Macros);
        _engine.SetConfiguration(Configuration);
        if (registerHotkeys)
        {
            _hotkeys.RegisterAll(Configuration.Settings);
        }
        StartupService.SetEnabled(Configuration.Settings.StartWithWindows);
        SaveConfiguration();
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
        UpdateStatus();
    }

    public void SaveConfiguration()
    {
        try
        {
            _store.Save(Configuration);
        }
        catch (Exception exception)
        {
            _logger.Error("保存配置失败。", exception);
            StatusText = "保存配置失败，请查看日志";
        }
    }

    public void ImportConfiguration(string path)
    {
        var imported = _store.Import(path);
        Configuration = imported;
        _profileResolver.SelectManual(Configuration, Configuration.ActiveProfileId);
        CommitConfiguration();
        _logger.Info($"Imported configuration from {path}.");
    }

    public void ExportConfiguration(string path)
    {
        _store.Export(Configuration, path);
        _logger.Info($"Exported configuration to {path}.");
    }

    public void StartMacroRecording(MacroDefinition macro)
    {
        if (!Configuration.Macros.Contains(macro))
        {
            Configuration.Macros.Add(macro);
        }

        _typingWheel.SetActive(false);
        _recordingMacro = macro;
        _recordingWasSuspended = _engine.IsSuspended;
        _engine.SetSuspended(true);
        _macroRecorder.Start();
        StatusText = "正在录制宏；按 Ctrl+Alt+F10 或托盘暂停键停止";
    }

    public void StopMacroRecording()
    {
        if (!_macroRecorder.IsRecording)
        {
            return;
        }

        _macroRecorder.Stop();
        CommitConfiguration();
        _recordingMacro = null;
        _engine.SetSuspended(_recordingWasSuspended);
        StatusText = "宏录制已停止";
    }

    public void ShowTypingWheelPreview()
    {
        _typingWheelWindow?.ShowOverlay(new TypingWheelState(true, 0, TypingWheelLayout.Layers[0].Name, TypingWheelDirection.Up, false));
    }

    public void ShowWindow()
    {
        OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    public void RequestExit() => ExitRequested?.Invoke(this, EventArgs.Empty);

    private void WireEvents()
    {
        _gamepadSource.SnapshotReceived += OnSnapshotReceived;
        _hotkeys.Pressed += OnHotkeyPressed;
        _hotkeys.RegistrationFailed += (_, message) =>
        {
            _logger.Warning(message);
            StatusText = message;
        };
        _macroRecorder.StepRecorded += OnMacroStepRecorded;
        _macroRecorder.RecordingStopped += (_, _) =>
        {
            if (_recordingMacro is not null && !_engine.IsEnabled)
            {
                return;
            }
            UpdateStatus();
        };
        _statusTimer.Tick += (_, _) => RefreshState();
        _tray.OpenRequested += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        _tray.EnabledToggleRequested += (_, _) => ToggleEnabled();
        _tray.PauseToggleRequested += (_, _) => ToggleManualPause();
        _tray.ProfileRequested += (_, id) => SelectProfile(id, manual: true);
        _tray.ExitRequested += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSnapshotReceived(object? sender, GamepadSnapshot snapshot)
    {
        LastSnapshot = snapshot;
        _typingWheel.Process(snapshot, Configuration.Settings);
        if (_fullscreenEscapeOverride.Process(_fullscreen.IsFullscreen, _typingWheel.IsActive, snapshot.IsDown(GamepadControl.B)))
        {
            _inputDispatcher.Execute(new ActionDefinition
            {
                Type = ActionType.Keyboard,
                Key = KeyChordFactory.Of(KeyChordFactory.KeyEscape)
            });
        }
        _engine.SetSuspended(ShouldSuspendMapping());
        _engine.Process(snapshot);
        var now = DateTimeOffset.Now;
        if (now - _lastUiUpdate < TimeSpan.FromMilliseconds(33))
        {
            return;
        }

        _lastUiUpdate = now;
        _uiDispatcher?.BeginInvoke(() =>
        {
            ControllerText = snapshot.IsConnected ? $"手柄 {snapshot.ControllerIndex + 1} 已连接" : "未连接手柄";
            StateChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void OnMacroStepRecorded(object? sender, MacroStep step)
    {
        if (_recordingMacro is null)
        {
            return;
        }

        _recordingMacro.Steps.Add(step.Clone());
        MacroStepRecorded?.Invoke(this, step);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnHotkeyPressed(object? sender, HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.ToggleMapping:
                ToggleEnabled();
                break;
            case HotkeyAction.PanicStop:
                SetEnabled(false);
                break;
            case HotkeyAction.ShowWindow:
                OpenRequested?.Invoke(this, EventArgs.Empty);
                break;
            case HotkeyAction.StopRecording:
                StopMacroRecording();
                break;
        }
    }

    private void OnTypingWheelStateChanged(object? sender, TypingWheelState state)
    {
        _uiDispatcher?.BeginInvoke(() =>
        {
            if (_typingWheelWindow is null) return;
                if (state.IsActive) _typingWheelWindow.ShowOverlay(state);
            else _typingWheelWindow.Hide();
            UpdateStatus();
        });
    }

    private bool ShouldSuspendMapping()
    {
        return _manualPause || _typingWheel.IsActive || _macroRecorder.IsRecording || _uiInputCaptureActive;
    }

    private void RefreshState()
    {
        try
        {
            _foreground = _foregroundService.GetForegroundApp();
            _fullscreen = _fullscreenDetector.Detect();
            var nextProfileId = _profileResolver.Resolve(Configuration, _foreground);

            if (nextProfileId != Guid.Empty && nextProfileId != Configuration.ActiveProfileId)
            {
                _engine.SetProfile(nextProfileId);
                Configuration.ActiveProfileId = nextProfileId;
                ConfigurationChanged?.Invoke(this, EventArgs.Empty);
            }

            _engine.SetSuspended(ShouldSuspendMapping());
            UpdateStatus();
        }
        catch (Exception exception)
        {
            _logger.Error("状态刷新失败。", exception);
        }
    }

    private void UpdateStatus()
    {
        var state = !IsEnabled ? "已暂停（手动关闭）" :
            _typingWheel.IsActive ? "打字轮盘已开启" :
            _macroRecorder.IsRecording ? "正在录制宏" :
            _manualPause ? "已暂停" :
            _uiInputCaptureActive ? "正在捕获键盘输入" :
            "映射正在运行";
        StatusText = state;
        var active = Configuration.FindProfile(Configuration.ActiveProfileId);
        var tooltip = $"GamepadPilot - {state}";
        if (active is not null)
        {
            tooltip += $" - {active.Name}";
        }

        _tray.Update(Configuration.Profiles, Configuration.ActiveProfileId, IsEnabled, IsSuspended, tooltip);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool CheckAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName!);
    }

    public void Dispose()
    {
        _statusTimer.Stop();
        _engine.SetEnabled(false);
        _typingWheel.StateChanged -= OnTypingWheelStateChanged;
        _typingWheel.SetActive(false);
        _typingWheelWindow?.Close();
        _typingWheelWindow = null;
        _inputSink.ReleaseAll();
        _gamepadSource.Dispose();
        _macroRecorder.Dispose();
        _hotkeys.Dispose();
        _inputDispatcher.Dispose();
        _tray.Dispose();
        SaveConfiguration();
        _logger.Info("GamepadPilot stopped.");
    }
}
