namespace ScreenTranslator.Configuration;

public sealed class HotkeyOptions
{
    public const string SectionName = "HotKeys";

    public string Select { get; init; } = "Ctrl+Alt+T";
    public string RepeatLast { get; init; } = "Ctrl+Alt+R";
}
