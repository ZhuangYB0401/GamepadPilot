using GamepadPilot.Core.Domain;

namespace GamepadPilot.Core.Input;

public readonly record struct AnalogVector(double X, double Y, double Magnitude);

public static class AnalogMath
{
    public static AnalogVector Apply(short rawX, short rawY, StickSettings settings, AnalogVector? previous, double deltaSeconds)
    {
        var x = NormalizeAxis(rawX);
        var y = -NormalizeAxis(rawY);
        var magnitude = Math.Sqrt((x * x) + (y * y));
        if (magnitude <= settings.DeadZone || magnitude < 0.0001d)
        {
            return new AnalogVector(0d, 0d, 0d);
        }

        var normalizedMagnitude = Math.Clamp((magnitude - settings.DeadZone) / (1d - settings.DeadZone), 0d, 1d);
        var curved = Math.Pow(normalizedMagnitude, Math.Clamp(settings.Curve, 0.25d, 4d));
        var unitX = x / magnitude;
        var unitY = y / magnitude;
        var targetX = unitX * curved;
        var targetY = unitY * curved;

        if (previous is { } old)
        {
            var alpha = Math.Clamp(1d - settings.Smoothing, 0.05d, 1d);
            targetX = old.X + ((targetX - old.X) * alpha);
            targetY = old.Y + ((targetY - old.Y) * alpha);
        }

        return new AnalogVector(targetX, targetY, Math.Clamp(Math.Sqrt((targetX * targetX) + (targetY * targetY)), 0d, 1d));
    }

    private static double NormalizeAxis(short value) =>
        Math.Clamp(value >= 0 ? value / 32767d : value / 32768d, -1d, 1d);
}
