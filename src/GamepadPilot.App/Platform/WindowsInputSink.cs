using System.Runtime.InteropServices;
using GamepadPilot.Core.Domain;
using GamepadPilot.Core.Services;
using GamepadPilot.Core.Typing;

namespace GamepadPilot.App.Platform;

public sealed class WindowsInputSink : IMacroOutput, ITextInputSink
{
    private static readonly UIntPtr InjectedMarker = new(0x47505054u);
    private readonly object _gate = new();
    private readonly Dictionary<uint, int> _keyCounts = [];
    private readonly Dictionary<MouseButtonKind, int> _buttonCounts = [];
    private double _mouseRemainderX;
    private double _mouseRemainderY;
    private double _wheelRemainderX;
    private double _wheelRemainderY;
    private double _volumeRemainder;

    public void Execute(ActionDefinition action)
    {
        switch (action.Type)
        {
            case ActionType.Keyboard:
            case ActionType.Media:
            case ActionType.Browser:
            case ActionType.System:
                TapChord(action.Key);
                break;
            case ActionType.MouseButton when action.Hold:
                MouseButtonDown(action.MouseButton);
                break;
            case ActionType.MouseButton:
                TapMouseButton(action.MouseButton, doubleClick: action.ClickCount >= 2);
                break;
            case ActionType.MouseWheel:
                SendWheel(ActionWheelDelta(action.WheelDirection), 0);
                break;
            case ActionType.MouseMove:
                SendMouseMove((int)Math.Round(action.Scale), 0);
                break;
        }
    }

    public void Press(ActionDefinition action)
    {
        switch (action.Type)
        {
            case ActionType.Keyboard:
            case ActionType.Media:
            case ActionType.Browser:
            case ActionType.System:
                if (action.Key is not null) PressChord(action.Key);
                break;
            case ActionType.MouseButton:
                MouseButtonDown(action.MouseButton);
                break;
            case ActionType.MouseWheel:
                SendWheel(ActionWheelDelta(action.WheelDirection), 0);
                break;
        }
    }

    public void Release(ActionDefinition action)
    {
        switch (action.Type)
        {
            case ActionType.Keyboard:
            case ActionType.Media:
            case ActionType.Browser:
            case ActionType.System:
                ReleaseChord(action.Key);
                break;
            case ActionType.MouseButton:
                MouseButtonUp(action.MouseButton);
                break;
        }
    }

    public void Continuous(ActionDefinition action, double deltaX, double deltaY)
    {
        switch (action.Type)
        {
            case ActionType.MouseMove:
                _mouseRemainderX += deltaX;
                _mouseRemainderY += deltaY;
                var moveX = (int)Math.Round(_mouseRemainderX, MidpointRounding.AwayFromZero);
                var moveY = (int)Math.Round(_mouseRemainderY, MidpointRounding.AwayFromZero);
                if (moveX != 0 || moveY != 0)
                {
                    _mouseRemainderX -= moveX;
                    _mouseRemainderY -= moveY;
                    SendMouseMove(moveX, moveY);
                }
                break;
            case ActionType.Volume:
                ApplyVolumeDelta(deltaY);
                break;
            case ActionType.MouseWheel:
                _wheelRemainderX += deltaX;
                _wheelRemainderY += deltaY;
                var wheelX = (int)Math.Truncate(_wheelRemainderX);
                var wheelY = (int)Math.Truncate(_wheelRemainderY);
                if (wheelX != 0 || wheelY != 0)
                {
                    _wheelRemainderX -= wheelX;
                    _wheelRemainderY -= wheelY;
                    SendWheel(wheelY * 120, wheelX * 120);
                }
                break;
        }
    }

    public void KeyDown(uint virtualKey)
    {
        if (virtualKey == 0)
        {
            return;
        }

        lock (_gate)
        {
            _keyCounts.TryGetValue(virtualKey, out var count);
            _keyCounts[virtualKey] = count + 1;
            if (count == 0)
            {
                SendKeyboard(virtualKey, keyUp: false);
            }
        }
    }

    public void KeyUp(uint virtualKey)
    {
        if (virtualKey == 0)
        {
            return;
        }

        lock (_gate)
        {
            if (!_keyCounts.TryGetValue(virtualKey, out var count))
            {
                return;
            }

            if (count <= 1)
            {
                _keyCounts.Remove(virtualKey);
                SendKeyboard(virtualKey, keyUp: true);
            }
            else
            {
                _keyCounts[virtualKey] = count - 1;
            }
        }
    }

    public void MouseButtonDown(MouseButtonKind button)
    {
        lock (_gate)
        {
            _buttonCounts.TryGetValue(button, out var count);
            _buttonCounts[button] = count + 1;
            if (count == 0)
            {
                SendMouseButton(button, down: true);
            }
        }
    }

    public void MouseButtonUp(MouseButtonKind button)
    {
        lock (_gate)
        {
            if (!_buttonCounts.TryGetValue(button, out var count))
            {
                return;
            }

            if (count <= 1)
            {
                _buttonCounts.Remove(button);
                SendMouseButton(button, down: false);
            }
            else
            {
                _buttonCounts[button] = count - 1;
            }
        }
    }

    public void MouseWheel(int verticalDelta, int horizontalDelta) => SendWheel(verticalDelta, horizontalDelta);

    public void MouseMove(int deltaX, int deltaY)
    {
        if (deltaX != 0 || deltaY != 0)
        {
            SendMouseMove(deltaX, deltaY);
        }
    }

    public void SendGlyph(ITypingGlyph glyph)
    {
        switch (glyph.Kind)
        {
            case TypingGlyphKind.Space:
                SendKeyboard(KeyChordFactory.KeySpace, keyUp: false);
                SendKeyboard(KeyChordFactory.KeySpace, keyUp: true);
                break;
            case TypingGlyphKind.Backspace:
                SendKeyboard(KeyChordFactory.KeyBackspace, keyUp: false);
                SendKeyboard(KeyChordFactory.KeyBackspace, keyUp: true);
                break;
            case TypingGlyphKind.Enter:
                SendKeyboard(KeyChordFactory.KeyEnter, keyUp: false);
                SendKeyboard(KeyChordFactory.KeyEnter, keyUp: true);
                break;
            case TypingGlyphKind.Character when glyph.Character is { } character:
                SendCharacter(character);
                break;
        }
    }

    public void ReleaseAll()
    {
        lock (_gate)
        {
            foreach (var key in _keyCounts.Keys.ToArray())
            {
                SendKeyboard(key, keyUp: true);
            }
            foreach (var button in _buttonCounts.Keys.ToArray())
            {
                SendMouseButton(button, down: false);
            }
            _keyCounts.Clear();
            _buttonCounts.Clear();
            _mouseRemainderX = 0d;
            _mouseRemainderY = 0d;
            _wheelRemainderX = 0d;
            _wheelRemainderY = 0d;
        }
    }

    private void TapChord(KeyChord? chord)
    {
        if (chord is null || chord.VirtualKey == 0)
        {
            return;
        }

        PressChord(chord);
        Thread.Sleep(12);
        ReleaseChord(chord);
    }

    private void PressChord(KeyChord chord)
    {
        if (chord.Control) KeyDown(0x11);
        if (chord.Alt) KeyDown(0x12);
        if (chord.Shift) KeyDown(0x10);
        if (chord.Windows) KeyDown(0x5B);
        KeyDown(chord.VirtualKey);
    }

    private void ReleaseChord(KeyChord? chord)
    {
        if (chord is null || chord.VirtualKey == 0)
        {
            return;
        }

        KeyUp(chord.VirtualKey);
        if (chord.Windows) KeyUp(0x5B);
        if (chord.Shift) KeyUp(0x10);
        if (chord.Alt) KeyUp(0x12);
        if (chord.Control) KeyUp(0x11);
    }

    private void TapMouseButton(MouseButtonKind button, bool doubleClick)
    {
        MouseButtonDown(button);
        Thread.Sleep(12);
        MouseButtonUp(button);
        if (doubleClick)
        {
            Thread.Sleep(35);
            MouseButtonDown(button);
            Thread.Sleep(12);
            MouseButtonUp(button);
        }
    }

    private static int ActionWheelDelta(MouseWheelDirection direction) => direction switch
    {
        MouseWheelDirection.Up => 120,
        MouseWheelDirection.Down => -120,
        _ => 0
    };

    private static void SendKeyboard(uint virtualKey, bool keyUp)
    {
        var input = new Input
        {
            Type = InputKeyboard,
            Union = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = (ushort)virtualKey,
                    ScanCode = (ushort)MapVirtualKeyEx(virtualKey, 0, GetForegroundKeyboardLayout()),
                    Flags = (keyUp ? KeyEventKeyUp : 0u) | (IsExtendedKey(virtualKey) ? KeyEventExtendedKey : 0u),
                    ExtraInfo = InjectedMarker
                }
            }
        };
        Send([input]);
    }

    private void SendCharacter(char character)
    {
        if (character <= 0x7F && TrySendKeyboardCharacter(character))
        {
            return;
        }

        SendUnicode(character, keyUp: false);
        SendUnicode(character, keyUp: true);
    }

    private bool TrySendKeyboardCharacter(char character)
    {
        var layout = GetForegroundKeyboardLayout();
        var keyScan = VkKeyScanEx(character, layout);
        if (keyScan == -1)
        {
            return false;
        }

        var virtualKey = (uint)(keyScan & 0xFF);
        var state = (keyScan >> 8) & 0xFF;
        var control = (state & 0x02) != 0;
        var alt = (state & 0x04) != 0;
        var shift = (state & 0x01) != 0;
        if (control) KeyDown(0x11);
        if (alt) KeyDown(0x12);
        if (shift) KeyDown(0x10);
        KeyDown(virtualKey);
        KeyUp(virtualKey);
        if (shift) KeyUp(0x10);
        if (alt) KeyUp(0x12);
        if (control) KeyUp(0x11);
        return true;
    }

    private static IntPtr GetForegroundKeyboardLayout()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return GetKeyboardLayout(0);
        }

        var threadId = GetWindowThreadProcessId(window, out _);
        return GetKeyboardLayout(threadId);
    }

    private static void SendUnicode(char character, bool keyUp)
    {
        var input = new Input
        {
            Type = InputKeyboard,
            Union = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = 0,
                    ScanCode = (ushort)character,
                    Flags = KeyEventUnicode | (keyUp ? KeyEventKeyUp : 0u),
                    ExtraInfo = InjectedMarker
                }
            }
        };
        Send([input]);
    }

    private static void SendMouseButton(MouseButtonKind button, bool down)
    {
        uint flag;
        uint data = 0;
        switch (button)
        {
            case MouseButtonKind.Left:
                flag = down ? MouseEventLeftDown : MouseEventLeftUp;
                break;
            case MouseButtonKind.Right:
                flag = down ? MouseEventRightDown : MouseEventRightUp;
                break;
            case MouseButtonKind.Middle:
                flag = down ? MouseEventMiddleDown : MouseEventMiddleUp;
                break;
            case MouseButtonKind.XButton1:
                flag = down ? MouseEventXDown : MouseEventXUp;
                data = XButton1;
                break;
            case MouseButtonKind.XButton2:
                flag = down ? MouseEventXDown : MouseEventXUp;
                data = XButton2;
                break;
            default:
                return;
        }
        SendMouse(0, 0, data, flag);
    }

    private void ApplyVolumeDelta(double delta)
    {
        lock (_gate)
        {
            _volumeRemainder += delta;
            var steps = (int)Math.Truncate(_volumeRemainder);
            if (steps == 0) return;
            _volumeRemainder -= steps;
            steps = Math.Clamp(steps, -8, 8);
            var virtualKey = steps > 0 ? KeyChordFactory.KeyVolumeUp : KeyChordFactory.KeyVolumeDown;
            for (var index = 0; index < Math.Abs(steps); index++)
            {
                SendKeyboard(virtualKey, keyUp: false);
                SendKeyboard(virtualKey, keyUp: true);
            }
        }
    }

    private static void SendMouseMove(int deltaX, int deltaY) => SendMouse(deltaX, deltaY, 0, MouseEventMove | MouseEventMoveNoCoalesce);
    private static void SendWheel(int verticalDelta, int horizontalDelta)
    {
        if (verticalDelta != 0)
        {
            SendMouse(0, 0, unchecked((uint)verticalDelta), MouseEventWheel);
        }
        if (horizontalDelta != 0)
        {
            SendMouse(0, 0, unchecked((uint)horizontalDelta), MouseEventHWheel);
        }
    }

    private static void SendMouse(int x, int y, uint data, uint flags)
    {
        var input = new Input
        {
            Type = InputMouse,
            Union = new InputUnion
            {
                Mouse = new MouseInput
                {
                    DeltaX = x,
                    DeltaY = y,
                    MouseData = data,
                    Flags = flags,
                    ExtraInfo = InjectedMarker
                }
            }
        };
        Send([input]);
    }

    private static void Send(Input[] inputs)
    {
        var size = Marshal.SizeOf<Input>();
        if (SendInput((uint)inputs.Length, inputs, size) == 0)
        {
            throw new InvalidOperationException($"SendInput failed: {Marshal.GetLastWin32Error()}");
        }
    }

    private static bool IsExtendedKey(uint virtualKey) => virtualKey is
        0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28 or 0x2D or 0x2E or
        0x5B or 0x5C or 0xA3 or 0xA5 or 0xA6 or 0xA7 or 0xAD or 0xAE or 0xAF or
        0xB0 or 0xB1 or 0xB2 or 0xB3;

    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventUnicode = 0x0004;
    private const uint KeyEventExtendedKey = 0x0001;
    private const uint MouseEventMove = 0x0001;
    private const uint MouseEventMoveNoCoalesce = 0x2000;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;
    private const uint MouseEventMiddleDown = 0x0020;
    private const uint MouseEventMiddleUp = 0x0040;
    private const uint MouseEventXDown = 0x0080;
    private const uint MouseEventXUp = 0x0100;
    private const uint MouseEventWheel = 0x0800;
    private const uint MouseEventHWheel = 0x1000;
    private const uint XButton1 = 0x0001;
    private const uint XButton2 = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int DeltaX;
        public int DeltaY;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern short VkKeyScanEx(char character, IntPtr keyboardLayout);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKeyEx(uint code, uint mapType, IntPtr keyboardLayout);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int size);
}
