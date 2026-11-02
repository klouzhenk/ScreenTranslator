namespace ScreenTranslator.Configuration;

internal sealed class OcrOptions
{
    public const string SectionName = "Ocr";
    public string Language { get; init; } = "en-US";
}
