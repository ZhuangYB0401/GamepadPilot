using GamepadPilot.Core.Domain;

namespace GamepadPilot.Core.Configuration;

public static class DefaultConfigurationFactory
{
    public static readonly Guid DesktopProfileId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid BrowserProfileId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid BlankProfileId = Guid.Parse("30000000-0000-0000-0000-000000000003");

    public static AppConfiguration Create()
    {
        var desktop = BuildDesktopPreset();
        var browser = BuildBrowserPreset();
        var blank = new ProfileDefinition
        {
            Id = BlankProfileId,
            Name = "空白配置",
            IsSystem = true
        };
        return new AppConfiguration
        {
            SchemaVersion = 6,
            Profiles = [desktop, browser, blank],
            DefaultProfileId = desktop.Id,
            ActiveProfileId = desktop.Id,
            AppRules = CreateBrowserRules(browser.Id)
        };
    }

    public static ProfileDefinition BuildBrowserPreset()
    {
        var profile = BuildDesktopPreset();
        profile.Id = BrowserProfileId;
        profile.Name = "浏览器预设（Chrome / Edge）";
        profile.Bindings.RemoveAll(x => x.Source is GamepadControl.LeftBumper or GamepadControl.RightBumper or GamepadControl.LeftTrigger or GamepadControl.RightTrigger);
        profile.Bindings.Add(Press(GamepadControl.LeftBumper, MouseButton(MouseButtonKind.XButton1)));
        profile.Bindings.Add(Press(GamepadControl.RightBumper, MouseButton(MouseButtonKind.XButton2)));
        profile.Bindings.Add(Continuous(GamepadControl.LeftTrigger, ActionType.Volume, 1d, 1d, VolumeDirection.Down));
        profile.Bindings.Add(Continuous(GamepadControl.RightTrigger, ActionType.Volume, 1d, 1d, VolumeDirection.Up));
        return profile;
    }

    public static List<AppRule> CreateBrowserRules(Guid profileId) =>
    [
        new AppRule { ProcessName = "chrome", ProfileId = profileId, Order = 0 },
        new AppRule { ProcessName = "msedge", ProfileId = profileId, Order = 1 }
    ];

    private static ProfileDefinition BuildDesktopPreset()
    {
        var profile = new ProfileDefinition
        {
            Id = DesktopProfileId,
            Name = "桌面操控预设",
            IsSystem = true,
            LeftStick = new StickSettings { DeadZone = 0.15d, Sensitivity = 1500d, Curve = 1d, Smoothing = 0.05d },
            RightStick = new StickSettings { DeadZone = 0.15d, Sensitivity = 900d, Curve = 1d, ScrollRate = 16d, Smoothing = 0.05d, InvertY = true },
            Bindings =
            [
                Continuous(GamepadControl.LeftStick, ActionType.MouseMove, 1d, 1d),
                Continuous(GamepadControl.RightStick, ActionType.MouseWheel, 1d, 1d),
                Hold(GamepadControl.A, MouseButton(MouseButtonKind.Left)),
                Hold(GamepadControl.B, MouseButton(MouseButtonKind.Right)),
                Hold(GamepadControl.X, Keyboard(KeyChordFactory.KeyEnter)),
                Hold(GamepadControl.Y, Keyboard(KeyChordFactory.KeyEscape)),
                Press(GamepadControl.DPadUp, Keyboard(KeyChordFactory.KeyUp)),
                Press(GamepadControl.DPadDown, Keyboard(KeyChordFactory.KeyDown)),
                Press(GamepadControl.DPadLeft, Keyboard(KeyChordFactory.KeyLeft)),
                Press(GamepadControl.DPadRight, Keyboard(KeyChordFactory.KeyRight)),
                Press(GamepadControl.LeftBumper, Keyboard(KeyChordFactory.KeyBrowserBack)),
                Press(GamepadControl.RightBumper, Keyboard(KeyChordFactory.KeyBrowserForward)),
                Press(GamepadControl.LeftTrigger, Keyboard(KeyChordFactory.KeyPageUp)),
                Press(GamepadControl.RightTrigger, Keyboard(KeyChordFactory.KeyPageDown)),
                Press(GamepadControl.LeftThumb, Keyboard(KeyChordFactory.KeyLeftWindows)),
                Press(GamepadControl.RightThumb, Keyboard(KeyChordFactory.KeyTab, windows: true)),
                Press(GamepadControl.Back, Keyboard(KeyChordFactory.KeyBackspace)),
                Press(GamepadControl.Start, Keyboard(KeyChordFactory.KeyEnter))
            ]
        };
        return profile;
    }

    private static ControlBinding Press(GamepadControl source, ActionDefinition action) => new()
    {
        Source = source,
        Trigger = TriggerMode.Press,
        Action = action
    };

    private static ControlBinding Hold(GamepadControl source, ActionDefinition action) => new()
    {
        Source = source,
        Trigger = TriggerMode.Hold,
        Action = action
    };

    private static ControlBinding Continuous(
        GamepadControl source,
        ActionType type,
        double scale,
        double curve,
        VolumeDirection volumeDirection = VolumeDirection.Up) => new()
    {
        Source = source,
        Trigger = TriggerMode.Continuous,
        Scale = scale,
        Action = new ActionDefinition
        {
            Type = type,
            Scale = scale,
            Curve = curve,
            VolumeDirection = volumeDirection
        }
    };

    private static ActionDefinition Keyboard(uint key, bool control = false, bool alt = false, bool shift = false, bool windows = false) => new()
    {
        Type = ActionType.Keyboard,
        Key = KeyChordFactory.Of(key, control, alt, shift, windows)
    };

    private static ActionDefinition MouseButton(MouseButtonKind button) => new()
    {
        Type = ActionType.MouseButton,
        MouseButton = button
    };

    public static ActionDefinition BuiltIn(ActionType type, uint key = 0, MouseButtonKind mouseButton = MouseButtonKind.Left) => type switch
    {
        ActionType.Keyboard => new ActionDefinition { Type = type, Key = KeyChordFactory.Of(key) },
        ActionType.MouseButton => new ActionDefinition { Type = type, MouseButton = mouseButton },
        ActionType.MouseWheel => new ActionDefinition { Type = type, WheelDirection = MouseWheelDirection.Up },
        ActionType.MouseMove => new ActionDefinition { Type = type, Scale = 1d, Curve = 1.5d },
        ActionType.Volume => new ActionDefinition { Type = type, Scale = 1d, Curve = 1d },
        ActionType.Media or ActionType.Browser or ActionType.System => new ActionDefinition { Type = type, Key = KeyChordFactory.Of(key) },
        _ => new ActionDefinition()
    };
}
