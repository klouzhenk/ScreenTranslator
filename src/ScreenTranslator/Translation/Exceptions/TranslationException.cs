namespace ScreenTranslator.Translation.Exceptions;

internal sealed class TranslationException(TranslationErrorKind kind, string? details, Exception inner) : Exception(details ?? kind.ToString(), inner)
{
    public TranslationErrorKind Kind { get; } = kind;
}
