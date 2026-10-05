namespace GamepadPilot.Core.Input;

public sealed class FullscreenEscapeOverride
{
    private bool _wasBPressed;

    public bool Process(bool isFullscreen, bool typingWheelActive, bool bPressed)
    {
        var fireEscape = isFullscreen && !typingWheelActive && bPressed && !_wasBPressed;
        _wasBPressed = bPressed;
        return fireEscape;
    }
}
