using GamepadPilot.Core.Domain;
using GamepadPilot.Core.Typing;

namespace GamepadPilot.Core.Services;

public interface IGamepadSource : IDisposable
{
    event EventHandler<GamepadSnapshot>? SnapshotReceived;
    GamepadSnapshot Current { get; }
    bool IsRunning { get; }
    void Start();
    void Stop();
}

public interface IMacroOutput
{
    void KeyDown(uint virtualKey);
    void KeyUp(uint virtualKey);
    void MouseButtonDown(MouseButtonKind button);
    void MouseButtonUp(MouseButtonKind button);
    void MouseWheel(int verticalDelta, int horizontalDelta);
    void MouseMove(int deltaX, int deltaY);
}

public interface IActionDispatcher
{
    void Execute(ActionDefinition action);
    void Press(ActionDefinition action);
    void Release(ActionDefinition action);
    void Continuous(ActionDefinition action, double x, double y, double seconds);
    void ReleaseAll();
}

public interface ITextInputSink
{
    void SendGlyph(ITypingGlyph glyph);
}

public interface ITypingGlyph
{
    string Label { get; }
    TypingGlyphKind Kind { get; }
    char? Character { get; }
}

public interface IForegroundAppService
{
    ForegroundAppInfo? GetForegroundApp();
}

public interface IFullscreenDetector
{
    FullscreenState Detect();
}

public interface IProfileStore
{
    string ConfigPath { get; }
    AppConfiguration Load();
    void Save(AppConfiguration configuration);
    void Export(AppConfiguration configuration, string path);
    AppConfiguration Import(string path);
}

public interface IMacroRecorder : IDisposable
{
    bool IsRecording { get; }
    event EventHandler<MacroStep>? StepRecorded;
    event EventHandler? RecordingStopped;
    void Start();
    void Stop();
}

public sealed record ForegroundAppInfo(int ProcessId, string ProcessName, string WindowTitle, string? ExecutablePath);
public sealed record FullscreenState(bool IsFullscreen, string Reason);

public static class ControlCatalog
{
    public static readonly IReadOnlyList<GamepadControl> ButtonControls =
    [
        GamepadControl.A, GamepadControl.B, GamepadControl.X, GamepadControl.Y,
        GamepadControl.LeftBumper, GamepadControl.RightBumper, GamepadControl.Back,
        GamepadControl.Start, GamepadControl.LeftThumb, GamepadControl.RightThumb,
        GamepadControl.DPadUp, GamepadControl.DPadDown, GamepadControl.DPadLeft,
        GamepadControl.DPadRight, GamepadControl.LeftTrigger, GamepadControl.RightTrigger,
        GamepadControl.BackPaddle1, GamepadControl.BackPaddle2, GamepadControl.BackPaddle3, GamepadControl.BackPaddle4
    ];

    public static readonly IReadOnlyList<GamepadControl> AllControls =
        [.. ButtonControls, GamepadControl.LeftStick, GamepadControl.RightStick];

    public static string DisplayName(GamepadControl control) => control switch
    {
        GamepadControl.A => "A",
        GamepadControl.B => "B",
        GamepadControl.X => "X",
        GamepadControl.Y => "Y",
        GamepadControl.LeftBumper => "LB",
        GamepadControl.RightBumper => "RB",
        GamepadControl.Back => "Back",
        GamepadControl.Start => "Start",
        GamepadControl.LeftThumb => "L3",
        GamepadControl.RightThumb => "R3",
        GamepadControl.DPadUp => "方向键上",
        GamepadControl.DPadDown => "方向键下",
        GamepadControl.DPadLeft => "方向键左",
        GamepadControl.DPadRight => "方向键右",
        GamepadControl.LeftTrigger => "LT",
        GamepadControl.RightTrigger => "RT",
        GamepadControl.LeftStick => "左摇杆",
        GamepadControl.RightStick => "右摇杆",
        GamepadControl.BackPaddle1 => "背键 1",
        GamepadControl.BackPaddle2 => "背键 2",
        GamepadControl.BackPaddle3 => "背键 3",
        GamepadControl.BackPaddle4 => "背键 4",
        _ => control.ToString()
    };

    public static bool IsContinuousControl(GamepadControl control) =>
        control is GamepadControl.LeftStick or GamepadControl.RightStick;
}
