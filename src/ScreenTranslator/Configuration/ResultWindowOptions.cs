namespace ScreenTranslator.Configuration;

internal sealed class ResultWindowOptions
{
    public const string SectionName = "ResultWindow";
    
    public int AutoHideSeconds { get; init; } = 10;
    public double FontSize { get; init; } = 16;
}