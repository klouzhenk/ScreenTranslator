namespace ScreenTranslator.Configuration;

public sealed class TranslationOptions
{
    public const string SectionName = "Translation";
    public string TargetLang { get; init; } = "UK";
    public string? SourceLang { get; init; }
}
