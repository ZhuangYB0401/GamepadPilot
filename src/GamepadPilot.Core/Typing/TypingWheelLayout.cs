namespace GamepadPilot.Core.Typing;

public enum TypingWheelDirection
{
    Right = 0,
    UpRight = 1,
    Up = 2,
    UpLeft = 3,
    Left = 4,
    DownLeft = 5,
    Down = 6,
    DownRight = 7
}

public enum TypingGlyphKind
{
    Character,
    Space,
    Backspace,
    Enter
}

public sealed record TypingGlyph(
    string Label,
    TypingGlyphKind Kind,
    char? Character = null,
    char? ShiftCharacter = null) : GamepadPilot.Core.Services.ITypingGlyph;

public sealed record TypingWheelLayer(string Name, IReadOnlyList<IReadOnlyList<TypingGlyph>> Sectors);

public sealed record TypingWheelState(
    bool IsActive,
    int LayerIndex,
    string LayerName,
    TypingWheelDirection Direction,
    bool ShiftActive)
{
    public static TypingWheelState Inactive { get; } = new(false, 0, string.Empty, TypingWheelDirection.Up, false);
}

public static class TypingWheelLayout
{
    public static IReadOnlyList<TypingWheelLayer> Layers { get; } =
    [
        new TypingWheelLayer("字母与常用字符", BuildLetterLayer()),
        new TypingWheelLayer("数字与符号", BuildSymbolLayer())
    ];

    public static TypingGlyph GetGlyph(int layerIndex, TypingWheelDirection direction, int slot)
    {
        var layer = Layers[Math.Clamp(layerIndex, 0, Layers.Count - 1)];
        return layer.Sectors[(int)direction][Math.Clamp(slot, 0, 3)];
    }

    public static TypingGlyph ResolveGlyph(int layerIndex, TypingWheelDirection direction, int slot, bool shift)
    {
        var glyph = GetGlyph(layerIndex, direction, slot);
        return shift && glyph.ShiftCharacter is { } shifted
            ? glyph with { Character = shifted }
            : glyph;
    }

    public static string DisplayLabel(int layerIndex, TypingWheelDirection direction, int slot, bool shift)
    {
        var glyph = ResolveGlyph(layerIndex, direction, slot, shift);
        return glyph.Character?.ToString() ?? glyph.Label;
    }

    private static IReadOnlyList<IReadOnlyList<TypingGlyph>> BuildLetterLayer()
    {
        TypingGlyph Ch(char value, char? shift = null) => new(value.ToString(), TypingGlyphKind.Character, value, shift);
        return
        [
            [Ch('y', 'Y'), Ch('z', 'Z'), Ch(',', '<'), Ch('.', '>')],
            [Ch('/', '?'), Ch('?'), Ch('-', '_'), Ch('=', '+')],
            [Ch('a', 'A'), Ch('b', 'B'), Ch('c', 'C'), Ch('d', 'D')],
            [Ch('e', 'E'), Ch('f', 'F'), Ch('g', 'G'), Ch('h', 'H')],
            [Ch('i', 'I'), Ch('j', 'J'), Ch('k', 'K'), Ch('l', 'L')],
            [Ch('m', 'M'), Ch('n', 'N'), Ch('o', 'O'), Ch('p', 'P')],
            [Ch('q', 'Q'), Ch('r', 'R'), Ch('s', 'S'), Ch('t', 'T')],
            [Ch('u', 'U'), Ch('v', 'V'), Ch('w', 'W'), Ch('x', 'X')]
        ];
    }

    private static IReadOnlyList<IReadOnlyList<TypingGlyph>> BuildSymbolLayer()
    {
        TypingGlyph Ch(char value, char? shift = null) => new(value.ToString(), TypingGlyphKind.Character, value, shift);
        return
        [
            [Ch(';', ':'), Ch('\'', '"'), Ch('[', '{'), Ch(']', '}')],
            [Ch(',', '<'), Ch('.', '>'), Ch('/', '?'), Ch('?')],
            [Ch('0', ')'), Ch('1', '!'), Ch('2', '@'), Ch('3', '#')],
            [Ch('4', '$'), Ch('5', '%'), Ch('6', '^'), Ch('7', '&')],
            [Ch('8', '*'), Ch('9', '('), Ch('!'), Ch('@')],
            [Ch('#'), Ch('$'), Ch('%'), Ch('^')],
            [Ch('&'), Ch('*'), Ch('('), Ch(')')],
            [Ch('-', '_'), Ch('=', '+'), Ch('[', '{'), Ch(']', '}')]
        ];
    }
}
