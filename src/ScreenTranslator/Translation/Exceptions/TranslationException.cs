namespace ScreenTranslator.Translation.Exceptions;

public sealed class TranslationException(TranslationErrorKind kind, string? details, Exception inner) : Exception(details ?? kind.ToString(), inner)
{
    public TranslationErrorKind Kind { get; } = kind;
}
