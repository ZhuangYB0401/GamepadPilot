namespace GamepadPilot.Core.Domain;

public enum TriggerMode { Press, Hold, LongPress, DoublePress, Continuous }

public enum ActionType
{
    None,
    Keyboard,
    MouseButton,
    MouseWheel,
    MouseMove,
    Volume,
    Media,
    Browser,
    System,
    Macro
}

public enum MouseButtonKind { Left, Right, Middle, XButton1, XButton2 }
public enum MouseWheelDirection { Up, Down, Left, Right }
public enum VolumeDirection { Up, Down }
public enum MacroRepeatMode { Once, Count, WhileHeld, Toggle }
public enum MacroStepType { Delay, KeyDown, KeyUp, MouseButtonDown, MouseButtonUp, MouseWheel, MouseMove }

public sealed class KeyChord
{
    public uint VirtualKey { get; set; }
    public bool Control { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public bool Windows { get; set; }

    public KeyChord Clone() => new()
    {
        VirtualKey = VirtualKey,
        Control = Control,
        Alt = Alt,
        Shift = Shift,
        Windows = Windows
    };
}

public sealed class ActionDefinition
{
    public ActionType Type { get; set; } = ActionType.None;
    public KeyChord? Key { get; set; }
    public MouseButtonKind MouseButton { get; set; } = MouseButtonKind.Left;
    public MouseWheelDirection WheelDirection { get; set; } = MouseWheelDirection.Up;
    public VolumeDirection VolumeDirection { get; set; } = VolumeDirection.Up;
    public Guid? MacroId { get; set; }
    public bool Hold { get; set; }
    public int ClickCount { get; set; } = 1;
    public double Scale { get; set; } = 1d;
    public double Curve { get; set; } = 1.5d;

    public ActionDefinition Clone() => new()
    {
        Type = Type,
        Key = Key?.Clone(),
        MouseButton = MouseButton,
        WheelDirection = WheelDirection,
        VolumeDirection = VolumeDirection,
        MacroId = MacroId,
        Hold = Hold,
        ClickCount = ClickCount,
        Scale = Scale,
        Curve = Curve
    };
}

public sealed class ControlBinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public GamepadControl Source { get; set; } = GamepadControl.A;
    public TriggerMode Trigger { get; set; } = TriggerMode.Press;
    public bool Enabled { get; set; } = true;
    public double Scale { get; set; } = 1d;
    public ActionDefinition Action { get; set; } = new();
}

public sealed class ComboBinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "新组合";
    public List<GamepadControl> Buttons { get; set; } = [];
    public TriggerMode Trigger { get; set; } = TriggerMode.Press;
    public bool Enabled { get; set; } = true;
    public ActionDefinition Action { get; set; } = new();
}

public sealed class StickSettings
{
    public double DeadZone { get; set; } = 0.15d;
    public double Sensitivity { get; set; } = 1100d;
    public double Curve { get; set; } = 1.5d;
    public double ScrollRate { get; set; } = 14d;
    public double Smoothing { get; set; } = 0.18d;
    public bool InvertY { get; set; }
}

public sealed class MacroStep
{
    public MacroStepType Type { get; set; } = MacroStepType.Delay;
    public int DelayMs { get; set; } = 50;
    public uint VirtualKey { get; set; }
    public bool Control { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public bool Windows { get; set; }
    public MouseButtonKind MouseButton { get; set; } = MouseButtonKind.Left;
    public int WheelDelta { get; set; }
    public int DeltaX { get; set; }
    public int DeltaY { get; set; }

    public MacroStep Clone() => (MacroStep)MemberwiseClone();
}

public sealed class MacroDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "新宏";
    public MacroRepeatMode RepeatMode { get; set; } = MacroRepeatMode.Once;
    public int RepeatCount { get; set; } = 2;
    public double Speed { get; set; } = 1d;
    public List<MacroStep> Steps { get; set; } = [];
}

public sealed class ProfileDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "新配置";
    public bool IsSystem { get; set; }
    public List<ControlBinding> Bindings { get; set; } = [];
    public List<ComboBinding> Combos { get; set; } = [];
    public StickSettings LeftStick { get; set; } = new();
    public StickSettings RightStick { get; set; } = new() { Sensitivity = 900d, ScrollRate = 14d };
}

public sealed class AppRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProcessName { get; set; } = string.Empty;
    public string? TitleContains { get; set; }
    public Guid ProfileId { get; set; }
    public bool Enabled { get; set; } = true;
    public int Order { get; set; }
}

public sealed class AppSettings
{
    public bool AutoPauseFullscreen { get; set; }
    public bool TypingWheelEnabled { get; set; } = true;
    public GamepadControl TypingWheelToggleControl { get; set; } = GamepadControl.LeftThumb;
    public double TypingWheelDirectionThreshold { get; set; } = 0.45d;
    public double TypingWheelTriggerThreshold { get; set; } = 0.55d;
    public string TypingWheelBackgroundColor { get; set; } = "#1A2433";
    public double TypingWheelBackgroundOpacity { get; set; } = 0.88d;
    public bool StartWithWindows { get; set; }
    public bool ShowWindowOnLaunch { get; set; } = true;
    public int DoublePressWindowMs { get; set; } = 250;
    public int LongPressWindowMs { get; set; } = 550;
    public KeyChord ToggleHotkey { get; set; } = KeyChordFactory.F12(control: true, alt: true);
    public KeyChord PanicHotkey { get; set; } = KeyChordFactory.End(control: true, alt: true);
    public KeyChord ShowHotkey { get; set; } = KeyChordFactory.F11(control: true, alt: true);
    public KeyChord RecordStopHotkey { get; set; } = KeyChordFactory.F10(control: true, alt: true);
}

public sealed class AppConfiguration
{
    public int SchemaVersion { get; set; } = 6;
    public Guid DefaultProfileId { get; set; }
    public Guid ActiveProfileId { get; set; }
    public List<ProfileDefinition> Profiles { get; set; } = [];
    public List<AppRule> AppRules { get; set; } = [];
    public List<MacroDefinition> Macros { get; set; } = [];
    public AppSettings Settings { get; set; } = new();

    public ProfileDefinition? FindProfile(Guid id) => Profiles.FirstOrDefault(x => x.Id == id);
    public MacroDefinition? FindMacro(Guid? id) => id is null ? null : Macros.FirstOrDefault(x => x.Id == id.Value);
}

public static class KeyChordFactory
{
    public const uint KeyBackspace = 0x08;
    public const uint KeyTab = 0x09;
    public const uint KeyEnter = 0x0D;
    public const uint KeyEscape = 0x1B;
    public const uint KeySpace = 0x20;
    public const uint KeyPageUp = 0x21;
    public const uint KeyPageDown = 0x22;
    public const uint KeyEnd = 0x23;
    public const uint KeyHome = 0x24;
    public const uint KeyLeft = 0x25;
    public const uint KeyUp = 0x26;
    public const uint KeyRight = 0x27;
    public const uint KeyDown = 0x28;
    public const uint KeyF10 = 0x79;
    public const uint KeyF11 = 0x7A;
    public const uint KeyF12 = 0x7B;
    public const uint KeyLeftWindows = 0x5B;
    public const uint KeyBrowserBack = 0xA6;
    public const uint KeyBrowserForward = 0xA7;
    public const uint KeyVolumeMute = 0xAD;
    public const uint KeyVolumeDown = 0xAE;
    public const uint KeyVolumeUp = 0xAF;
    public const uint KeyMediaNext = 0xB0;
    public const uint KeyMediaPrevious = 0xB1;
    public const uint KeyMediaStop = 0xB2;
    public const uint KeyMediaPlayPause = 0xB3;

    public static KeyChord Of(uint key, bool control = false, bool alt = false, bool shift = false, bool windows = false) =>
        new() { VirtualKey = key, Control = control, Alt = alt, Shift = shift, Windows = windows };

    public static KeyChord F10(bool control = false, bool alt = false, bool shift = false, bool windows = false) => Of(KeyF10, control, alt, shift, windows);
    public static KeyChord F11(bool control = false, bool alt = false, bool shift = false, bool windows = false) => Of(KeyF11, control, alt, shift, windows);
    public static KeyChord F12(bool control = false, bool alt = false, bool shift = false, bool windows = false) => Of(KeyF12, control, alt, shift, windows);
    public static KeyChord End(bool control = false, bool alt = false, bool shift = false, bool windows = false) => Of(KeyEnd, control, alt, shift, windows);
}
