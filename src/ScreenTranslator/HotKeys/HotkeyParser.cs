using System.Windows.Input;

namespace ScreenTranslator.HotKeys;

public static class HotkeyParser
{
    public static (uint Modifiers, uint VirtualKey) Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Hotkey is empty", nameof(input));

        var parts = input.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
            throw new FormatException($"Hotkey '{input}' must contain at least one modifier and one key.");

        var modifiers = ParseModifiers(parts[..^1], input);
        var virtualKey = ParseKey(parts[^1], input);

        return (modifiers, virtualKey);
    }

    private static uint ParseModifiers(string[] tokens, string original)
    {
        uint modifiers = 0;
        foreach (var token in tokens)
        {
            modifiers |= token.ToLowerInvariant() switch
            {
                "ctrl" or "control" => NativeMethods.MOD_CONTROL,
                "alt"               => NativeMethods.MOD_ALT,
                "shift"             => NativeMethods.MOD_SHIFT,
                "win" or "windows"  => NativeMethods.MOD_WIN,
                _                   => throw new FormatException($"Unknown modifier: '{token}' in '{original}'")
            };
        }
        return modifiers;
    }

    private static uint ParseKey(string token, string original)
    {
        if (!Enum.TryParse<Key>(token, ignoreCase: true, out var key) || key is Key.None)
            throw new FormatException($"Unknown key: '{token}' in '{original}'");

        return (uint)KeyInterop.VirtualKeyFromKey(key);
    }
}
