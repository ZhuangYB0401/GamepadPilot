using System.Diagnostics;
using System.Runtime.InteropServices;
using GamepadPilot.Core.Domain;
using GamepadPilot.Core.Services;

namespace GamepadPilot.App.Platform;

public sealed class WindowsMacroRecorder : IMacroRecorder
{
    private readonly object _gate = new();
    private readonly Stopwatch _clock = new();
    private readonly LowLevelKeyboardProc _keyboardCallback;
    private readonly LowLevelMouseProc _mouseCallback;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private long _lastEventTicks;

    public WindowsMacroRecorder()
    {
        _keyboardCallback = KeyboardHook;
        _mouseCallback = MouseHook;
    }

    public bool IsRecording { get; private set; }
    public event EventHandler<MacroStep>? StepRecorded;
    public event EventHandler? RecordingStopped;

    public void Start()
    {
        lock (_gate)
        {
            if (IsRecording)
            {
                return;
            }

            using var process = Process.GetCurrentProcess();
            using var module = process.MainModule;
            var moduleHandle = GetModuleHandle(module?.ModuleName);
            _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardCallback, moduleHandle, 0);
            _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseCallback, moduleHandle, 0);
            if (_keyboardHook == IntPtr.Zero || _mouseHook == IntPtr.Zero)
            {
                StopHooks();
                throw new InvalidOperationException("无法启动全局输入录制钩子。");
            }

            _clock.Restart();
            _lastEventTicks = 0;
            IsRecording = true;
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!IsRecording)
            {
                return;
            }

            StopHooks();
            _clock.Stop();
            IsRecording = false;
            RecordingStopped?.Invoke(this, EventArgs.Empty);
        }
    }

    private IntPtr KeyboardHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code < 0)
        {
            return CallNextHookEx(_keyboardHook, code, wParam, lParam);
        }

        var data = Marshal.PtrToStructure<KeyboardHookData>(lParam);
        if (data.ExtraInfo == InjectedMarker || (data.Flags & LlkhfInjected) != 0 || IsOwnWindowForeground())
        {
            return CallNextHookEx(_keyboardHook, code, wParam, lParam);
        }

        var isDown = wParam.ToInt32() is WmKeyDown or WmSysKeyDown;
        var isUp = wParam.ToInt32() is WmKeyUp or WmSysKeyUp;
        if (isDown || isUp)
        {
            Emit(new MacroStep
            {
                Type = isDown ? MacroStepType.KeyDown : MacroStepType.KeyUp,
                VirtualKey = data.VirtualKey
            });
        }

        return CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    private IntPtr MouseHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code < 0)
        {
            return CallNextHookEx(_mouseHook, code, wParam, lParam);
        }

        var data = Marshal.PtrToStructure<MouseHookData>(lParam);
        if (data.ExtraInfo == InjectedMarker || IsOwnWindowForeground())
        {
            return CallNextHookEx(_mouseHook, code, wParam, lParam);
        }

        var message = wParam.ToInt32();
        MacroStep? step;
        if (message is WmXButtonDown or WmXButtonUp)
        {
            var button = (data.MouseData >> 16) == 2 ? MouseButtonKind.XButton2 : MouseButtonKind.XButton1;
            step = new MacroStep
            {
                Type = message == WmXButtonDown ? MacroStepType.MouseButtonDown : MacroStepType.MouseButtonUp,
                MouseButton = button
            };
        }
        else
        {
            step = message switch
            {
            WmLeftButtonDown => new MacroStep { Type = MacroStepType.MouseButtonDown, MouseButton = MouseButtonKind.Left },
            WmLeftButtonUp => new MacroStep { Type = MacroStepType.MouseButtonUp, MouseButton = MouseButtonKind.Left },
            WmRightButtonDown => new MacroStep { Type = MacroStepType.MouseButtonDown, MouseButton = MouseButtonKind.Right },
            WmRightButtonUp => new MacroStep { Type = MacroStepType.MouseButtonUp, MouseButton = MouseButtonKind.Right },
            WmMiddleButtonDown => new MacroStep { Type = MacroStepType.MouseButtonDown, MouseButton = MouseButtonKind.Middle },
            WmMiddleButtonUp => new MacroStep { Type = MacroStepType.MouseButtonUp, MouseButton = MouseButtonKind.Middle },
            WmMouseWheel => new MacroStep { Type = MacroStepType.MouseWheel, WheelDelta = unchecked((short)(data.MouseData >> 16)) },
                _ => null
            };
        }

        if (step is not null)
        {
            Emit(step);
        }

        return CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private void Emit(MacroStep step)
    {
        var elapsed = _clock.ElapsedMilliseconds - _lastEventTicks;
        _lastEventTicks = _clock.ElapsedMilliseconds;
        if (elapsed > 4)
        {
            StepRecorded?.Invoke(this, new MacroStep { Type = MacroStepType.Delay, DelayMs = (int)Math.Min(elapsed, 5000) });
        }
        StepRecorded?.Invoke(this, step);
    }

    private static bool IsOwnWindowForeground()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return false;
        }

        GetWindowThreadProcessId(window, out var processId);
        return processId == (uint)Environment.ProcessId;
    }

    private void StopHooks()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
    }

    public void Dispose() => Stop();

    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int WmLeftButtonDown = 0x0201;
    private const int WmLeftButtonUp = 0x0202;
    private const int WmRightButtonDown = 0x0204;
    private const int WmRightButtonUp = 0x0205;
    private const int WmMiddleButtonDown = 0x0207;
    private const int WmMiddleButtonUp = 0x0208;
    private const int WmMouseWheel = 0x020A;
    private const int WmXButtonDown = 0x020B;
    private const int WmXButtonUp = 0x020C;
    private const uint LlkhfInjected = 0x10;
    private static readonly UIntPtr InjectedMarker = new(0x47505054u);

    private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);
    private delegate IntPtr LowLevelMouseProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardHookData
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookData
    {
        public Point Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, Delegate callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
