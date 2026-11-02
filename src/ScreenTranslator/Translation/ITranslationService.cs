namespace ScreenTranslator.Translation;

internal interface ITranslationService
{
    Task<string> TranslateAsync(string text, CancellationToken cancellationToken = default);
}
