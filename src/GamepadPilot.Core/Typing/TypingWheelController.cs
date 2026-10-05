using GamepadPilot.Core.Domain;
using GamepadPilot.Core.Services;

namespace GamepadPilot.Core.Typing;

public sealed class TypingWheelController
{
    private readonly ITextInputSink _textInput;
    private bool _active;
    private int _layerIndex;
    private TypingWheelDirection _direction = TypingWheelDirection.Up;
    private bool _toggleLatched;
    private bool _aDown;
    private bool _bDown;
    private bool _xDown;
    private bool _yDown;
    private bool _shiftActive;
    private bool _layerSwitchLatched;
    private bool _spaceDown;
    private bool _backspaceDown;
    private bool _enterDown;
    private DateTimeOffset _spaceRepeatAt;
    private DateTimeOffset _backspaceRepeatAt;

    public TypingWheelController(ITextInputSink textInput)
    {
        _textInput = textInput;
    }

    public event EventHandler<TypingWheelState>? StateChanged;
    public bool IsActive => _active;
    public TypingWheelState State => _active
        ? new TypingWheelState(true, _layerIndex, TypingWheelLayout.Layers[_layerIndex].Name, _direction, _shiftActive)
        : TypingWheelState.Inactive;

    public TypingWheelState Process(GamepadSnapshot snapshot, AppSettings settings)
    {
        if (!settings.TypingWheelEnabled || !snapshot.IsConnected)
        {
            SetActive(false);
            return State;
        }

        var toggleDown = snapshot.IsDown(settings.TypingWheelToggleControl);
        if (toggleDown && !_toggleLatched)
        {
            SetActive(!_active);
            if (_active)
            {
                _layerIndex = 0;
                _direction = TypingWheelDirection.Up;
                _shiftActive = false;
                SyncLatches(snapshot, settings);
                RaiseStateChanged();
            }
        }
        _toggleLatched = toggleDown;

        if (_active)
        {
            UpdateDirection(snapshot, settings.TypingWheelDirectionThreshold);
            UpdateLayer(snapshot, settings.TypingWheelDirectionThreshold);
            UpdateShift(snapshot, settings.TypingWheelTriggerThreshold);
            UpdateSpecialKeys(snapshot, settings.TypingWheelTriggerThreshold);
            UpdateTyping(snapshot);
        }

        return State;
    }

    public void SetActive(bool active)
    {
        if (_active == active)
        {
            return;
        }

        _active = active;
        if (!active)
        {
            _aDown = _bDown = _xDown = _yDown = false;
            _shiftActive = false;
            _layerSwitchLatched = false;
            _spaceDown = _backspaceDown = _enterDown = false;
            _spaceRepeatAt = _backspaceRepeatAt = default;
        }
        RaiseStateChanged();
    }

    private void UpdateDirection(GamepadSnapshot snapshot, double threshold)
    {
        var direction = GetDirectionFromDPad(snapshot);
        if (direction is not null)
        {
            SetDirection(direction.Value);
            return;
        }

        var x = NormalizeAxis(snapshot.LeftThumbX);
        var y = NormalizeAxis(snapshot.LeftThumbY);
        var magnitude = Math.Sqrt((x * x) + (y * y));
        if (magnitude < Math.Clamp(threshold, 0.15d, 0.9d))
        {
            return;
        }

        var angle = Math.Atan2(y, x);
        var sector = (int)Math.Round(angle / (Math.PI / 4d), MidpointRounding.AwayFromZero);
        if (sector < 0) sector += 8;
        if (sector >= 8) sector -= 8;
        SetDirection((TypingWheelDirection)sector);
    }

    private void UpdateLayer(GamepadSnapshot snapshot, double threshold)
    {
        var switchThreshold = Math.Clamp(threshold, 0.35d, 0.9d);
        var x = NormalizeAxis(snapshot.RightThumbX);
        if (Math.Abs(x) >= switchThreshold && !_layerSwitchLatched)
        {
            CycleLayer(x < 0 ? -1 : 1);
            _layerSwitchLatched = true;
        }
        else if (Math.Abs(x) < switchThreshold * 0.6d)
        {
            _layerSwitchLatched = false;
        }
    }

    private void UpdateShift(GamepadSnapshot snapshot, double threshold)
    {
        var shiftThreshold = Math.Min(Math.Clamp(threshold, 0.2d, 0.95d), 0.45d);
        var shift = snapshot.LeftTrigger / 255d >= shiftThreshold;
        if (_shiftActive != shift)
        {
            _shiftActive = shift;
            RaiseStateChanged();
        }
    }

    private void UpdateSpecialKeys(GamepadSnapshot snapshot, double threshold)
    {
        var now = snapshot.Timestamp;
        UpdateRepeatingKey(
            snapshot.Buttons.HasFlag(GamepadButtons.LeftBumper),
            ref _spaceDown,
            ref _spaceRepeatAt,
            now,
            new TypingGlyph("空格", TypingGlyphKind.Space));
        UpdateRepeatingKey(
            snapshot.Buttons.HasFlag(GamepadButtons.RightBumper),
            ref _backspaceDown,
            ref _backspaceRepeatAt,
            now,
            new TypingGlyph("退格", TypingGlyphKind.Backspace));

        var enterDown = snapshot.RightTrigger / 255d >= threshold;
        if (enterDown && !_enterDown)
        {
            SendGlyph(new TypingGlyph("回车", TypingGlyphKind.Enter));
        }
        _enterDown = enterDown;
    }

    private void UpdateRepeatingKey(
        bool down,
        ref bool wasDown,
        ref DateTimeOffset repeatAt,
        DateTimeOffset now,
        TypingGlyph glyph)
    {
        if (down && !wasDown)
        {
            SendGlyph(glyph);
            repeatAt = now.AddMilliseconds(450);
        }
        else if (down && now >= repeatAt)
        {
            SendGlyph(glyph);
            repeatAt = now.AddMilliseconds(45);
        }
        else if (!down)
        {
            repeatAt = default;
        }
        wasDown = down;
    }

    private void UpdateTyping(GamepadSnapshot snapshot)
    {
        TypeOnPress(snapshot.Buttons.HasFlag(GamepadButtons.A), ref _aDown, 0);
        TypeOnPress(snapshot.Buttons.HasFlag(GamepadButtons.B), ref _bDown, 1);
        TypeOnPress(snapshot.Buttons.HasFlag(GamepadButtons.X), ref _xDown, 2);
        TypeOnPress(snapshot.Buttons.HasFlag(GamepadButtons.Y), ref _yDown, 3);
    }

    private void TypeOnPress(bool down, ref bool wasDown, int slot)
    {
        if (down && !wasDown)
        {
            var glyph = TypingWheelLayout.ResolveGlyph(_layerIndex, _direction, slot, _shiftActive);
            SendGlyph(glyph);
        }
        wasDown = down;
    }

    private void SendGlyph(ITypingGlyph glyph)
    {
        try
        {
            _textInput.SendGlyph(glyph);
        }
        catch
        {
        }
    }

    private void CycleLayer(int delta)
    {
        _layerIndex = (_layerIndex + delta + TypingWheelLayout.Layers.Count) % TypingWheelLayout.Layers.Count;
        RaiseStateChanged();
    }

    private void SetDirection(TypingWheelDirection direction)
    {
        if (_direction == direction)
        {
            return;
        }

        _direction = direction;
        RaiseStateChanged();
    }

    private void SyncLatches(GamepadSnapshot snapshot, AppSettings settings)
    {
        _aDown = snapshot.Buttons.HasFlag(GamepadButtons.A);
        _bDown = snapshot.Buttons.HasFlag(GamepadButtons.B);
        _xDown = snapshot.Buttons.HasFlag(GamepadButtons.X);
        _yDown = snapshot.Buttons.HasFlag(GamepadButtons.Y);
        _spaceDown = snapshot.Buttons.HasFlag(GamepadButtons.LeftBumper);
        _backspaceDown = snapshot.Buttons.HasFlag(GamepadButtons.RightBumper);
        _enterDown = snapshot.RightTrigger / 255d >= settings.TypingWheelTriggerThreshold;
        _shiftActive = snapshot.LeftTrigger / 255d >= Math.Min(settings.TypingWheelTriggerThreshold, 0.45d);
        _layerSwitchLatched = Math.Abs(NormalizeAxis(snapshot.RightThumbX)) >= Math.Clamp(settings.TypingWheelDirectionThreshold, 0.35d, 0.9d);
        _spaceRepeatAt = snapshot.Timestamp.AddMilliseconds(450);
        _backspaceRepeatAt = snapshot.Timestamp.AddMilliseconds(450);
    }

    private void RaiseStateChanged()
    {
        try
        {
            StateChanged?.Invoke(this, State);
        }
        catch
        {
        }
    }

    private static TypingWheelDirection? GetDirectionFromDPad(GamepadSnapshot snapshot)
    {
        var up = snapshot.Buttons.HasFlag(GamepadButtons.DPadUp);
        var down = snapshot.Buttons.HasFlag(GamepadButtons.DPadDown);
        var left = snapshot.Buttons.HasFlag(GamepadButtons.DPadLeft);
        var right = snapshot.Buttons.HasFlag(GamepadButtons.DPadRight);

        if (up && right) return TypingWheelDirection.UpRight;
        if (up && left) return TypingWheelDirection.UpLeft;
        if (down && right) return TypingWheelDirection.DownRight;
        if (down && left) return TypingWheelDirection.DownLeft;
        if (up) return TypingWheelDirection.Up;
        if (down) return TypingWheelDirection.Down;
        if (left) return TypingWheelDirection.Left;
        if (right) return TypingWheelDirection.Right;
        return null;
    }

    private static double NormalizeAxis(short value) =>
        Math.Clamp(value >= 0 ? value / 32767d : value / 32768d, -1d, 1d);
}
