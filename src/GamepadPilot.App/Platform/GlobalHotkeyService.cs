using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using GamepadPilot.Core.Domain;

namespace GamepadPilot.App.Platform;

public enum HotkeyAction
{
    ToggleMapping,
    PanicStop,
    ShowWindow,
    StopRecording
}

public sealed class GlobalHotkeyService : IDisposable
{
    private readonly Dictionary<int, HotkeyAction> _registered = [];
    private HwndSource? _source;
    private IntPtr _windowHandle;
    private int _nextId = 0x5100;

    public event EventHandler<HotkeyAction>? Pressed;
    public event EventHandler<string>? RegistrationFailed;

    public void Attach(Window window)
    {
        _windowHandle = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(WindowProcedure);
    }

    public void RegisterAll(AppSettings settings)
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        UnregisterAll();
        Register(settings.ToggleHotkey, HotkeyAction.ToggleMapping);
        Register(settings.PanicHotkey, HotkeyAction.PanicStop);
        Register(settings.ShowHotkey, HotkeyAction.ShowWindow);
        Register(settings.RecordStopHotkey, HotkeyAction.StopRecording);
    }

    private void Register(KeyChord? chord, HotkeyAction action)
    {
        if (chord is null || chord.VirtualKey == 0)
        {
            return;
        }

        var id = _nextId++;
        var modifiers = 0u;
        if (chord.Alt) modifiers |= ModAlt;
        if (chord.Control) modifiers |= ModControl;
        if (chord.Shift) modifiers |= ModShift;
        if (chord.Windows) modifiers |= ModWin;
        modifiers |= ModNoRepeat;

        if (RegisterHotKey(_windowHandle, id, modifiers, chord.VirtualKey))
        {
            _registered[id] = action;
        }
        else
        {
            RegistrationFailed?.Invoke(this, $"无法注册热键 {FormatChord(chord)}，可能已被其他程序占用。");
        }
    }

    public static string FormatChord(KeyChord chord)
    {
        var parts = new List<string>();
        if (chord.Control) parts.Add("Ctrl");
        if (chord.Alt) parts.Add("Alt");
        if (chord.Shift) parts.Add("Shift");
        if (chord.Windows) parts.Add("Win");
        parts.Add(FormatVirtualKey(chord.VirtualKey));
        return string.Join("+", parts);
    }

    private static string FormatVirtualKey(uint key)
    {
        if (key >= 0x70 && key <= 0x87) return $"F{key - 0x6F}";
        return key switch
        {
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x1B => "Esc",
            0x20 => "Space",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            >= 0x30 and <= 0x39 => ((char)key).ToString(),
            >= 0x41 and <= 0x5A => ((char)key).ToString(),
            _ => $"0x{key:X2}"
        };
    }

    private IntPtr WindowProcedure(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && _registered.TryGetValue(wParam.ToInt32(), out var action))
        {
            Pressed?.Invoke(this, action);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void UnregisterAll()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        foreach (var id in _registered.Keys)
        {
            UnregisterHotKey(_windowHandle, id);
        }
        _registered.Clear();
    }

    public void Dispose()
    {
        UnregisterAll();
        _source?.RemoveHook(WindowProcedure);
        _source = null;
        _windowHandle = IntPtr.Zero;
    }

    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
