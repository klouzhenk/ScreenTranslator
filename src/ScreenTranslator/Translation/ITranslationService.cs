namespace ScreenTranslator.Translation;

public interface ITranslationService
{
    Task<string> TranslateAsync(string text, CancellationToken cancellationToken = default);
}
