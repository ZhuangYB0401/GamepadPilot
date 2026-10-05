namespace GamepadPilot.Core.Domain;

public enum GamepadButton
{
    DPadUp = 0x0001,
    DPadDown = 0x0002,
    DPadLeft = 0x0004,
    DPadRight = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LeftThumb = 0x0040,
    RightThumb = 0x0080,
    LeftBumper = 0x0100,
    RightBumper = 0x0200,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000
}

[Flags]
public enum GamepadButtons : ushort
{
    None = 0,
    DPadUp = 0x0001,
    DPadDown = 0x0002,
    DPadLeft = 0x0004,
    DPadRight = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LeftThumb = 0x0040,
    RightThumb = 0x0080,
    LeftBumper = 0x0100,
    RightBumper = 0x0200,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000
}

public enum GamepadControl
{
    A = GamepadButton.A,
    B = GamepadButton.B,
    X = GamepadButton.X,
    Y = GamepadButton.Y,
    LeftBumper = GamepadButton.LeftBumper,
    RightBumper = GamepadButton.RightBumper,
    Back = GamepadButton.Back,
    Start = GamepadButton.Start,
    LeftThumb = GamepadButton.LeftThumb,
    RightThumb = GamepadButton.RightThumb,
    DPadUp = GamepadButton.DPadUp,
    DPadDown = GamepadButton.DPadDown,
    DPadLeft = GamepadButton.DPadLeft,
    DPadRight = GamepadButton.DPadRight,
    LeftTrigger,
    RightTrigger,
    LeftStick,
    RightStick,
    BackPaddle1 = 0x10000,
    BackPaddle2 = 0x10001,
    BackPaddle3 = 0x10002,
    BackPaddle4 = 0x10003
}

public readonly record struct GamepadSnapshot(
    bool IsConnected,
    int ControllerIndex,
    uint PacketNumber,
    GamepadButtons Buttons,
    byte LeftTrigger,
    byte RightTrigger,
    short LeftThumbX,
    short LeftThumbY,
    short RightThumbX,
    short RightThumbY,
    DateTimeOffset Timestamp)
{
    public static GamepadSnapshot Disconnected(DateTimeOffset timestamp) =>
        new(false, -1, 0, GamepadButtons.None, 0, 0, 0, 0, 0, 0, timestamp);

    public bool IsDown(GamepadControl control) => control switch
    {
        GamepadControl.LeftTrigger => LeftTrigger >= 85,
        GamepadControl.RightTrigger => RightTrigger >= 85,
        GamepadControl.LeftStick => Magnitude(LeftThumbX, LeftThumbY) >= 0.15,
        GamepadControl.RightStick => Magnitude(RightThumbX, RightThumbY) >= 0.15,
        GamepadControl.BackPaddle1 => (Buttons & (GamepadButtons)0x0400) != 0,
        GamepadControl.BackPaddle2 => (Buttons & (GamepadButtons)0x0800) != 0,
        GamepadControl.BackPaddle3 or GamepadControl.BackPaddle4 => false,
        _ => (Buttons & (GamepadButtons)(ushort)control) != 0
    };

    public double AxisValue(GamepadControl control) => control switch
    {
        GamepadControl.LeftTrigger => LeftTrigger / 255d,
        GamepadControl.RightTrigger => RightTrigger / 255d,
        GamepadControl.LeftStick => LeftStickMagnitude,
        GamepadControl.RightStick => RightStickMagnitude,
        _ => IsDown(control) ? 1d : 0d
    };

    public double LeftStickMagnitude => Magnitude(LeftThumbX, LeftThumbY);
    public double RightStickMagnitude => Magnitude(RightThumbX, RightThumbY);

    private static double Magnitude(short x, short y)
    {
        var nx = NormalizeAxis(x);
        var ny = NormalizeAxis(y);
        return Math.Clamp(Math.Sqrt((nx * nx) + (ny * ny)), 0d, 1d);
    }

    private static double NormalizeAxis(short value)
    {
        var normalized = value >= 0 ? value / 32767d : value / 32768d;
        return Math.Clamp(normalized, -1d, 1d);
    }
}
