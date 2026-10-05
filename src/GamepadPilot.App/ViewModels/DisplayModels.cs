using GamepadPilot.Core.Domain;
using GamepadPilot.Core.Services;

namespace GamepadPilot.App.ViewModels;

public sealed class OptionItem<T>(T value, string label)
{
    public T Value { get; } = value;
    public string Label { get; } = label;
    public override string ToString() => Label;
}

public sealed class BindingRow
{
    public BindingRow(ControlBinding binding) => Binding = binding;
    public ControlBinding Binding { get; }
    public string Source => ControlCatalog.DisplayName(Binding.Source);
    public string Trigger => DisplayFormat.Trigger(Binding.Trigger);
    public string Action => DisplayFormat.Action(Binding.Action);
    public bool Enabled { get => Binding.Enabled; set => Binding.Enabled = value; }
}

public sealed class ComboRow
{
    public ComboRow(ComboBinding binding) => Binding = binding;
    public ComboBinding Binding { get; }
    public string Name => Binding.Name;
    public string Buttons => string.Join(" + ", Binding.Buttons.Select(ControlCatalog.DisplayName));
    public string Trigger => DisplayFormat.Trigger(Binding.Trigger);
    public string Action => DisplayFormat.Action(Binding.Action);
    public bool Enabled { get => Binding.Enabled; set => Binding.Enabled = value; }
}

public sealed class MacroStepRow
{
    public MacroStepRow(int index, MacroStep step) { Index = index; Step = step; }
    public int Index { get; }
    public MacroStep Step { get; }
    public string TypeText => Step.Type switch
    {
        MacroStepType.Delay => "延迟",
        MacroStepType.KeyDown => "按键按下",
        MacroStepType.KeyUp => "按键松开",
        MacroStepType.MouseButtonDown => "鼠标按下",
        MacroStepType.MouseButtonUp => "鼠标松开",
        MacroStepType.MouseWheel => "鼠标滚轮",
        MacroStepType.MouseMove => "鼠标移动",
        _ => Step.Type.ToString()
    };
    public string Detail => Step.Type switch
    {
        MacroStepType.Delay => $"{Step.DelayMs} ms",
        MacroStepType.KeyDown or MacroStepType.KeyUp => DisplayFormat.Chord(new KeyChord { VirtualKey = Step.VirtualKey }),
        MacroStepType.MouseButtonDown or MacroStepType.MouseButtonUp => Step.MouseButton.ToString(),
        MacroStepType.MouseWheel => Step.WheelDelta.ToString(),
        MacroStepType.MouseMove => $"X {Step.DeltaX}, Y {Step.DeltaY}",
        _ => string.Empty
    };
}

public sealed class RuleRow
{
    public RuleRow(AppRule rule, string profileName) { Rule = rule; ProfileName = profileName; }
    public AppRule Rule { get; }
    public string ProcessName => Rule.ProcessName;
    public string TitleContains => Rule.TitleContains ?? string.Empty;
    public string ProfileName { get; }
    public bool Enabled { get => Rule.Enabled; set => Rule.Enabled = value; }
}

public static class DisplayFormat
{
    public static string Trigger(TriggerMode trigger) => trigger switch
    {
        TriggerMode.Press => "单击",
        TriggerMode.Hold => "按住",
        TriggerMode.LongPress => "长按",
        TriggerMode.DoublePress => "双击",
        TriggerMode.Continuous => "连续",
        _ => trigger.ToString()
    };

    public static string Action(ActionDefinition action) => action.Type switch
    {
        ActionType.None => "未绑定",
        ActionType.Keyboard => $"键盘 {Chord(action.Key)}",
        ActionType.MouseButton => $"鼠标 {MouseButton(action.MouseButton)}{(action.Hold ? "（按住）" : action.ClickCount >= 2 ? "（双击）" : string.Empty)}",
        ActionType.MouseWheel => "鼠标滚轮",
        ActionType.MouseMove => "鼠标移动",
        ActionType.Volume => $"音量 {(action.VolumeDirection == VolumeDirection.Up ? "增加" : "降低")}",
        ActionType.Media => $"媒体键 {Chord(action.Key)}",
        ActionType.Browser => $"浏览器 {Chord(action.Key)}",
        ActionType.System => $"系统 {Chord(action.Key)}",
        ActionType.Macro => "运行宏",
        _ => action.Type.ToString()
    };

    public static string Chord(KeyChord? chord)
    {
        if (chord is null || chord.VirtualKey == 0) return "未设置";
        var parts = new List<string>();
        if (chord.Control) parts.Add("Ctrl");
        if (chord.Alt) parts.Add("Alt");
        if (chord.Shift) parts.Add("Shift");
        if (chord.Windows) parts.Add("Win");
        parts.Add(KeyName(chord.VirtualKey));
        return string.Join("+", parts);
    }

    public static string KeyName(uint key)
    {
        if (key >= 0x70 && key <= 0x87) return $"F{key - 0x6F}";
        return key switch
        {
            0x08 => "Backspace", 0x09 => "Tab", 0x0D => "Enter", 0x1B => "Esc",
            0x20 => "Space", 0x21 => "PageUp", 0x22 => "PageDown", 0x23 => "End",
            0x24 => "Home", 0x25 => "Left", 0x26 => "Up", 0x27 => "Right", 0x28 => "Down",
            0xA6 => "BrowserBack", 0xA7 => "BrowserForward", 0xAD => "Mute",
            0xAE => "VolumeDown", 0xAF => "VolumeUp", 0xB0 => "MediaNext",
            0xB1 => "MediaPrevious", 0xB2 => "MediaStop", 0xB3 => "PlayPause",
            >= 0x30 and <= 0x39 => ((char)key).ToString(),
            >= 0x41 and <= 0x5A => ((char)key).ToString(),
            _ => $"VK 0x{key:X2}"
        };
    }

    private static string MouseButton(MouseButtonKind button) => button switch
    {
        MouseButtonKind.Left => "左键",
        MouseButtonKind.Right => "右键",
        MouseButtonKind.Middle => "中键",
        MouseButtonKind.XButton1 => "侧键 1",
        MouseButtonKind.XButton2 => "侧键 2",
        _ => button.ToString()
    };
}
