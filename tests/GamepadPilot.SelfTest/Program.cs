using GamepadPilot.Core.Configuration;
using GamepadPilot.Core.Domain;
using GamepadPilot.Core.Input;
using GamepadPilot.Core.Services;
using GamepadPilot.Core.Typing;

var tests = new List<(string Name, Func<Task> Run)>
{
    ("摇杆死区与曲线", TestAnalogMath),
    ("默认配置完整性", TestDefaultConfiguration),
    ("旧版默认配置迁移", TestConfigurationMigration),
    ("旧版鼠标响应迁移", TestMouseResponseMigration),
    ("按键映射执行", TestButtonMapping),
    ("组合按键优先", TestComboPrecedence),
    ("单击与双击消歧", TestDoublePress),
    ("右摇杆方向与线性音量", TestContinuousDefaults),
    ("打字轮盘布局与输入", TestTypingWheel),
    ("全屏 B 键退出", TestFullscreenEscape),
    ("配置 JSON 往返与恢复", TestConfigurationStore),
    ("应用规则匹配", TestProfileResolver),
    ("宏异步执行", TestMacroRunner)
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"[PASS] {test.Name}");
    }
    catch (Exception exception)
    {
        failures.Add($"{test.Name}: {exception.Message}");
        Console.WriteLine($"[FAIL] {test.Name}: {exception.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"通过 {tests.Count - failures.Count}/{tests.Count} 个测试。");
if (failures.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
    return 1;
}
return 0;

static Task TestAnalogMath()
{
    var settings = new StickSettings { DeadZone = 0.15d, Curve = 1.5d, Smoothing = 1d };
    var deadZone = AnalogMath.Apply(1500, 1500, settings, null, 0.008d);
    Assert.Equal(0d, deadZone.Magnitude, 0.001d);
    var full = AnalogMath.Apply(32767, 0, settings, null, 0.008d);
    Assert.True(full.X > 0.9d, "满偏左摇杆 X 应接近 1。");
    Assert.Equal(0d, full.Y, 0.001d);
    return Task.CompletedTask;
}

static Task TestDefaultConfiguration()
{
    var configuration = DefaultConfigurationFactory.Create();
    ConfigurationValidator.Normalize(configuration);
    Assert.Equal(3, configuration.Profiles.Count);
    Assert.Equal(6, configuration.SchemaVersion);
    Assert.True(!configuration.Settings.AutoPauseFullscreen, "全屏自动暂停应默认关闭。");
    var desktop = configuration.FindProfile(DefaultConfigurationFactory.DesktopProfileId)!;
    var browser = configuration.FindProfile(DefaultConfigurationFactory.BrowserProfileId)!;
    Assert.True(desktop.RightStick.InvertY, "桌面预设应反转右摇杆 Y 方向。");
    foreach (var source in new[] { GamepadControl.A, GamepadControl.B, GamepadControl.X, GamepadControl.Y })
    {
        Assert.True(desktop.Bindings.Any(x => x.Source == source && x.Trigger == TriggerMode.Hold), $"{source} 默认应为按住触发。");
    }
    Assert.True(desktop.Bindings.Any(x => x.Source == GamepadControl.LeftBumper && x.Action.Key?.VirtualKey == KeyChordFactory.KeyBrowserBack), "桌面预设 LB 应恢复为浏览器后退。");
    Assert.True(desktop.Bindings.Any(x => x.Source == GamepadControl.RightBumper && x.Action.Key?.VirtualKey == KeyChordFactory.KeyBrowserForward), "桌面预设 RB 应恢复为浏览器前进。");
    Assert.True(desktop.Bindings.Any(x => x.Source == GamepadControl.LeftTrigger && x.Action.Key?.VirtualKey == KeyChordFactory.KeyPageUp), "桌面预设 LT 应恢复为 Page Up。");
    Assert.True(desktop.Bindings.Any(x => x.Source == GamepadControl.RightTrigger && x.Action.Key?.VirtualKey == KeyChordFactory.KeyPageDown), "桌面预设 RT 应恢复为 Page Down。");
    Assert.True(desktop.Bindings.All(x => x.Source is not (GamepadControl.BackPaddle1 or GamepadControl.BackPaddle2 or GamepadControl.BackPaddle3 or GamepadControl.BackPaddle4)), "背键应默认留空。");
    Assert.True(browser.Bindings.Any(x => x.Source == GamepadControl.LeftBumper && x.Action.Type == ActionType.MouseButton && x.Action.MouseButton == MouseButtonKind.XButton1), "浏览器预设 LB 应为鼠标侧键 1。");
    Assert.True(browser.Bindings.Any(x => x.Source == GamepadControl.RightBumper && x.Action.Type == ActionType.MouseButton && x.Action.MouseButton == MouseButtonKind.XButton2), "浏览器预设 RB 应为鼠标侧键 2。");
    Assert.True(browser.Bindings.Any(x => x.Source == GamepadControl.LeftTrigger && x.Action.VolumeDirection == VolumeDirection.Down), "浏览器预设 LT 应降低音量。");
    Assert.True(browser.Bindings.Any(x => x.Source == GamepadControl.RightTrigger && x.Action.VolumeDirection == VolumeDirection.Up), "浏览器预设 RT 应提高音量。");
    Assert.True(configuration.AppRules.Any(x => x.ProcessName == "chrome" && x.ProfileId == browser.Id), "应自动切换到 Chrome 配置。");
    Assert.True(configuration.AppRules.Any(x => x.ProcessName == "msedge" && x.ProfileId == browser.Id), "应自动切换到 Edge 配置。");
    return Task.CompletedTask;
}

static Task TestButtonMapping()
{
    var configuration = new AppConfiguration
    {
        Profiles = [new ProfileDefinition
        {
            Name = "test",
            Bindings = [new ControlBinding
            {
                Source = GamepadControl.A,
                Trigger = TriggerMode.Press,
                Action = Keyboard(ActionType.Keyboard, 0x41)
            }]
        }]
    };
    ConfigurationValidator.Normalize(configuration);
    configuration.DefaultProfileId = configuration.Profiles[0].Id;
    configuration.ActiveProfileId = configuration.DefaultProfileId;
    var dispatcher = new RecordingDispatcher();
    var engine = new MappingEngine(configuration, dispatcher);
    var start = DateTimeOffset.Now;
    Assert.True(Snapshot(start, GamepadButtons.A).IsDown(GamepadControl.A), "A 位图应被识别。" );
    engine.Process(Snapshot(start, GamepadButtons.None));
    engine.Process(Snapshot(start.AddMilliseconds(10), GamepadButtons.A));
    Assert.Equal(1, dispatcher.Executed.Count);
    Assert.Equal(0x41u, dispatcher.Executed[0].Key!.VirtualKey);
    return Task.CompletedTask;
}

static Task TestComboPrecedence()
{
    var configuration = new AppConfiguration
    {
        Profiles = [new ProfileDefinition
        {
            Name = "test",
            Bindings =
            [
                new ControlBinding { Source = GamepadControl.A, Trigger = TriggerMode.Press, Action = Keyboard(ActionType.Keyboard, 0x41) },
                new ControlBinding { Source = GamepadControl.B, Trigger = TriggerMode.Press, Action = Keyboard(ActionType.Keyboard, 0x42) }
            ],
            Combos =
            [
                new ComboBinding
                {
                    Name = "A+B",
                    Buttons = [GamepadControl.A, GamepadControl.B],
                    Trigger = TriggerMode.Press,
                    Action = Keyboard(ActionType.Keyboard, 0x43)
                }
            ]
        }]
    };
    ConfigurationValidator.Normalize(configuration);
    configuration.DefaultProfileId = configuration.Profiles[0].Id;
    configuration.ActiveProfileId = configuration.DefaultProfileId;
    var dispatcher = new RecordingDispatcher();
    var engine = new MappingEngine(configuration, dispatcher);
    var start = DateTimeOffset.Now;
    engine.Process(Snapshot(start, GamepadButtons.None));
    engine.Process(Snapshot(start.AddMilliseconds(10), GamepadButtons.A));
    engine.Process(Snapshot(start.AddMilliseconds(20), GamepadButtons.A | GamepadButtons.B));
    Assert.Equal(1, dispatcher.Executed.Count);
    Assert.Equal(0x43u, dispatcher.Executed[0].Key!.VirtualKey);
    return Task.CompletedTask;
}

static Task TestDoublePress()
{
    var configuration = new AppConfiguration
    {
        Profiles = [new ProfileDefinition
        {
            Name = "test",
            Bindings =
            [
                new ControlBinding { Source = GamepadControl.A, Trigger = TriggerMode.Press, Action = Keyboard(ActionType.Keyboard, 0x41) },
                new ControlBinding { Source = GamepadControl.A, Trigger = TriggerMode.DoublePress, Action = Keyboard(ActionType.Keyboard, 0x42) }
            ]
        }]
    };
    ConfigurationValidator.Normalize(configuration);
    configuration.DefaultProfileId = configuration.Profiles[0].Id;
    configuration.ActiveProfileId = configuration.DefaultProfileId;
    var dispatcher = new RecordingDispatcher();
    var engine = new MappingEngine(configuration, dispatcher);
    var start = DateTimeOffset.Now;
    engine.Process(Snapshot(start, GamepadButtons.None));
    engine.Process(Snapshot(start.AddMilliseconds(10), GamepadButtons.A));
    engine.Process(Snapshot(start.AddMilliseconds(20), GamepadButtons.None));
    engine.Process(Snapshot(start.AddMilliseconds(100), GamepadButtons.A));
    Assert.Equal(1, dispatcher.Executed.Count);
    Assert.Equal(0x42u, dispatcher.Executed[0].Key!.VirtualKey);
    return Task.CompletedTask;
}

static Task TestContinuousDefaults()
{
    var configuration = DefaultConfigurationFactory.Create();
    configuration.ActiveProfileId = DefaultConfigurationFactory.BrowserProfileId;
    var dispatcher = new RecordingDispatcher();
    var engine = new MappingEngine(configuration, dispatcher);
    var start = DateTimeOffset.Now;
    engine.Process(Snapshot(start, GamepadButtons.None, rightY: 0, rightTrigger: 0));
    engine.Process(Snapshot(start.AddMilliseconds(10), GamepadButtons.None, rightY: 32767, rightTrigger: 0));
    Assert.True(dispatcher.ContinuousCalls.Any(x => x.Action.Type == ActionType.MouseWheel && x.Y > 0d), "右摇杆向上应产生页面向上的滚动增量。");

    engine.Process(Snapshot(start.AddMilliseconds(20), GamepadButtons.None, rightY: 0, rightTrigger: 0));
    dispatcher.ContinuousCalls.Clear();
    engine.Process(Snapshot(start.AddMilliseconds(30), GamepadButtons.None, rightY: 0, rightTrigger: 255));
    Assert.True(dispatcher.ContinuousCalls.Any(x => x.Action.Type == ActionType.Volume && x.Y > 0d), "RT 应产生音量增加增量。");

    engine.Process(Snapshot(start.AddMilliseconds(40), GamepadButtons.None, rightY: 0, rightTrigger: 0));
    dispatcher.ContinuousCalls.Clear();
    engine.Process(Snapshot(start.AddMilliseconds(50), GamepadButtons.None, rightY: 0, leftTrigger: 255));
    Assert.True(dispatcher.ContinuousCalls.Any(x => x.Action.Type == ActionType.Volume && x.Y < 0d), "LT 应产生音量降低增量。");
    return Task.CompletedTask;
}

static async Task TestConfigurationStore()
{
    var directory = Path.Combine(Path.GetTempPath(), "GamepadPilot-SelfTest-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    var path = Path.Combine(directory, "config.json");
    try
    {
        var store = new JsonProfileStore(path);
        var configuration = DefaultConfigurationFactory.Create();
        configuration.Settings.LongPressWindowMs = 620;
        store.Save(configuration);
        var loaded = store.Load();
        Assert.Equal(620, loaded.Settings.LongPressWindowMs);

        await File.WriteAllTextAsync(path, "{ not-json");
        var recovered = store.Load();
        Assert.Equal(3, recovered.Profiles.Count);
        Assert.True(Directory.EnumerateFiles(directory, "config.corrupt-*.json").Any(), "损坏配置应生成备份。");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static Task TestConfigurationMigration()
{
    var profile = new ProfileDefinition
    {
        Name = "桌面操控预设",
        IsSystem = true,
        Bindings =
        [
            new ControlBinding { Source = GamepadControl.A, Trigger = TriggerMode.Press, Action = new ActionDefinition { Type = ActionType.MouseButton, MouseButton = MouseButtonKind.Left } },
            new ControlBinding { Source = GamepadControl.LeftBumper, Trigger = TriggerMode.Press, Action = Keyboard(ActionType.Keyboard, KeyChordFactory.KeyBrowserBack) },
            new ControlBinding { Source = GamepadControl.RightTrigger, Trigger = TriggerMode.Press, Action = Keyboard(ActionType.Keyboard, KeyChordFactory.KeyPageDown) }
        ]
    };
    var configuration = new AppConfiguration
    {
        SchemaVersion = 1,
        Profiles = [profile],
        DefaultProfileId = profile.Id,
        ActiveProfileId = profile.Id
    };
    ConfigurationValidator.Normalize(configuration);
    Assert.Equal(6, configuration.SchemaVersion);
    Assert.True(profile.Bindings.Any(x => x.Source == GamepadControl.LeftBumper && x.Action.Key?.VirtualKey == KeyChordFactory.KeyBrowserBack), "旧版迁移后应恢复 LB 默认项。");
    Assert.True(profile.Bindings.Any(x => x.Source == GamepadControl.RightTrigger && x.Action.Key?.VirtualKey == KeyChordFactory.KeyPageDown), "旧版迁移后应恢复 RT 默认项。");
    Assert.True(profile.Bindings.Any(x => x.Source == GamepadControl.A && x.Trigger == TriggerMode.Hold), "旧版 A 默认项应迁移为按住。");
    Assert.True(profile.RightStick.InvertY, "旧版右摇杆默认项应启用反向。");
    Assert.True(configuration.Settings.TypingWheelEnabled, "迁移后应默认启用打字轮盘。");
    Assert.Equal(GamepadControl.LeftThumb, configuration.Settings.TypingWheelToggleControl);
    Assert.True(configuration.FindProfile(DefaultConfigurationFactory.BrowserProfileId) is not null, "迁移应增加浏览器配置。");
    return Task.CompletedTask;
}

static Task TestMouseResponseMigration()
{
    var profile = new ProfileDefinition
    {
        Id = DefaultConfigurationFactory.DesktopProfileId,
        Name = "桌面操控预设",
        IsSystem = true,
        LeftStick = new StickSettings { DeadZone = 0.15d, Sensitivity = 1100d, Curve = 1.5d, Smoothing = 0.18d },
        RightStick = new StickSettings { Curve = 1.5d, Smoothing = 0.18d, ScrollRate = 14d, InvertY = true },
        Bindings = [new ControlBinding { Source = GamepadControl.LeftStick, Trigger = TriggerMode.Continuous, Action = new ActionDefinition { Type = ActionType.MouseMove, Curve = 1.5d, Scale = 1d } }]
    };
    var configuration = new AppConfiguration
    {
        SchemaVersion = 3,
        Profiles = [profile],
        DefaultProfileId = profile.Id,
        ActiveProfileId = profile.Id
    };
    ConfigurationValidator.Normalize(configuration);
    Assert.Equal(6, configuration.SchemaVersion);
    Assert.Equal(1500d, profile.LeftStick.Sensitivity, 0.001d);
    Assert.Equal(1d, profile.LeftStick.Curve, 0.001d);
    Assert.Equal(0.05d, profile.LeftStick.Smoothing, 0.001d);
    Assert.Equal(1d, profile.Bindings[0].Action.Curve, 0.001d);
    return Task.CompletedTask;
}

static Task TestTypingWheel()
{
    Assert.Equal(2, TypingWheelLayout.Layers.Count);
    foreach (var layer in TypingWheelLayout.Layers)
    {
        Assert.Equal(8, layer.Sectors.Count);
        Assert.True(layer.Sectors.All(x => x.Count == 4), "每个轮盘区块必须包含四个字符。");
    }

    var settings = DefaultConfigurationFactory.Create().Settings;
    var output = new RecordingTextInputSink();
    var wheel = new TypingWheelController(output);
    var start = DateTimeOffset.Now;
    wheel.Process(Snapshot(start, GamepadButtons.None), settings);
    wheel.Process(Snapshot(start.AddMilliseconds(10), GamepadButtons.LeftThumb), settings);
    Assert.True(wheel.IsActive, "L3 应开启打字轮盘。");
    Assert.Equal(TypingWheelDirection.Up, wheel.State.Direction);

    wheel.Process(Snapshot(start.AddMilliseconds(20), GamepadButtons.LeftThumb | GamepadButtons.A), settings);
    Assert.True(output.Glyphs.Contains("a"), "第一层上方向 A 应输入 a。");

    wheel.Process(Snapshot(start.AddMilliseconds(30), GamepadButtons.LeftThumb), settings);
    wheel.Process(Snapshot(start.AddMilliseconds(40), GamepadButtons.LeftThumb, rightX: 32767), settings);
    Assert.Equal(1, wheel.State.LayerIndex);
    wheel.Process(Snapshot(start.AddMilliseconds(50), GamepadButtons.LeftThumb | GamepadButtons.A, rightX: 32767), settings);
    Assert.True(output.Glyphs.Contains("0"), "第二层上方向 A 应输入 0。");

    wheel.Process(Snapshot(start.AddMilliseconds(60), GamepadButtons.LeftThumb, rightX: 0), settings);
    wheel.Process(Snapshot(start.AddMilliseconds(70), GamepadButtons.LeftThumb, rightX: -32767), settings);
    Assert.Equal(0, wheel.State.LayerIndex);
    wheel.Process(Snapshot(start.AddMilliseconds(80), GamepadButtons.LeftThumb, leftX: 32767, leftTrigger: 255), settings);
    Assert.Equal(TypingWheelDirection.Right, wheel.State.Direction);
    Assert.True(wheel.State.ShiftActive, "LT 按住时应进入 Shift 状态。");
    wheel.Process(Snapshot(start.AddMilliseconds(90), GamepadButtons.LeftThumb | GamepadButtons.B, leftX: 32767, leftTrigger: 255), settings);
    Assert.True(output.Glyphs.Contains("Z"), "Shift 状态下应输入大写字母。");

    wheel.Process(Snapshot(start.AddMilliseconds(100), GamepadButtons.LeftThumb | GamepadButtons.LeftBumper, leftX: 32767), settings);
    Assert.True(output.Glyphs.Contains("空格"), "LB 应输入空格。");
    wheel.Process(Snapshot(start.AddMilliseconds(110), GamepadButtons.LeftThumb), settings);
    wheel.Process(Snapshot(start.AddMilliseconds(120), GamepadButtons.LeftThumb | GamepadButtons.RightBumper), settings);
    Assert.True(output.Glyphs.Contains("退格"), "RB 应输入退格。");
    wheel.Process(Snapshot(start.AddMilliseconds(130), GamepadButtons.LeftThumb), settings);
    wheel.Process(Snapshot(start.AddMilliseconds(140), GamepadButtons.LeftThumb, rightTrigger: 255), settings);
    Assert.True(output.Glyphs.Contains("回车"), "RT 应输入回车。");
    return Task.CompletedTask;
}

static Task TestFullscreenEscape()
{
    var escape = new FullscreenEscapeOverride();
    Assert.True(!escape.Process(false, false, true), "非全屏时 B 不应发送 Esc。");
    Assert.True(!escape.Process(true, false, true), "进入全屏时已按住的 B 不应补发 Esc。");
    Assert.True(!escape.Process(true, false, false), "B 松开时不应发送 Esc。");
    Assert.True(escape.Process(true, false, true), "全屏下重新按 B 应发送一次 Esc。");
    Assert.True(!escape.Process(true, true, false), "打字轮盘切换状态不应发送 Esc。");
    Assert.True(!escape.Process(true, true, true), "打字轮盘开启时 B 应继续输入字符。");
    return Task.CompletedTask;
}

static Task TestProfileResolver()
{
    var general = new ProfileDefinition { Name = "general" };
    var browser = new ProfileDefinition { Name = "browser" };
    var configuration = new AppConfiguration
    {
        Profiles = [general, browser],
        DefaultProfileId = general.Id,
        AppRules =
        [
            new AppRule { ProcessName = "chrome", ProfileId = general.Id, Order = 0 },
            new AppRule { ProcessName = "chrome", TitleContains = "工作台", ProfileId = browser.Id, Order = 1 }
        ]
    };
    ConfigurationValidator.Normalize(configuration);
    var resolver = new ProfileResolver();
    var resolved = resolver.Resolve(configuration, new ForegroundAppInfo(1, "chrome", "内部工作台 - Chrome", null));
    Assert.Equal(browser.Id, resolved);
    return Task.CompletedTask;
}

static async Task TestMacroRunner()
{
    var output = new RecordingMacroOutput();
    using var runner = new MacroRunner(output);
    var macro = new MacroDefinition
    {
        RepeatMode = MacroRepeatMode.Once,
        Steps =
        [
            new MacroStep { Type = MacroStepType.KeyDown, VirtualKey = 0x41 },
            new MacroStep { Type = MacroStepType.Delay, DelayMs = 20 },
            new MacroStep { Type = MacroStepType.KeyUp, VirtualKey = 0x41 }
        ]
    };
    runner.Start(macro);
    await Task.Delay(150);
    Assert.True(output.Events.Contains("down:41"), "宏应发送按键按下。");
    Assert.True(output.Events.Contains("up:41"), "宏应发送按键松开。");
}

static GamepadSnapshot Snapshot(
    DateTimeOffset time,
    GamepadButtons buttons,
    short leftX = 0,
    short leftY = 0,
    short rightX = 0,
    short rightY = 0,
    byte leftTrigger = 0,
    byte rightTrigger = 0) =>
    new(true, 0, (uint)time.ToUnixTimeMilliseconds(), buttons, leftTrigger, rightTrigger, leftX, leftY, rightX, rightY, time);

static ActionDefinition Keyboard(ActionType type, uint key) => new()
{
    Type = type,
    Key = KeyChordFactory.Of(key)
};

internal sealed class RecordingDispatcher : IActionDispatcher
{
    public List<ActionDefinition> Executed { get; } = [];
    public List<ActionDefinition> Pressed { get; } = [];
    public List<ActionDefinition> Released { get; } = [];
    public List<(ActionDefinition Action, double X, double Y)> ContinuousCalls { get; } = [];
    public void Execute(ActionDefinition action) => Executed.Add(action);
    public void Press(ActionDefinition action) => Pressed.Add(action);
    public void Release(ActionDefinition action) => Released.Add(action);
    public void Continuous(ActionDefinition action, double x, double y, double seconds) => ContinuousCalls.Add((action, x, y));
    public void ReleaseAll() { }
}

internal sealed class RecordingTextInputSink : ITextInputSink
{
    public List<string> Glyphs { get; } = [];
    public void SendGlyph(ITypingGlyph glyph) => Glyphs.Add(glyph.Character?.ToString() ?? glyph.Label);
}

internal sealed class RecordingMacroOutput : IMacroOutput
{
    public List<string> Events { get; } = [];
    public void KeyDown(uint virtualKey) => Events.Add($"down:{virtualKey:X2}");
    public void KeyUp(uint virtualKey) => Events.Add($"up:{virtualKey:X2}");
    public void MouseButtonDown(MouseButtonKind button) => Events.Add($"mdown:{button}");
    public void MouseButtonUp(MouseButtonKind button) => Events.Add($"mup:{button}");
    public void MouseWheel(int verticalDelta, int horizontalDelta) => Events.Add($"wheel:{verticalDelta}:{horizontalDelta}");
    public void MouseMove(int deltaX, int deltaY) => Events.Add($"move:{deltaX}:{deltaY}");
}

internal static class Assert
{
    public static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Equal<T>(T expected, T actual) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"期望 {expected}，实际 {actual}。");
        }
    }

    public static void Equal(double expected, double actual, double tolerance)
    {
        if (Math.Abs(expected - actual) > tolerance)
        {
            throw new InvalidOperationException($"期望 {expected}，实际 {actual}。");
        }
    }
}
