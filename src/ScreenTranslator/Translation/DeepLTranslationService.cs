using DeepL;
using Microsoft.Extensions.Options;
using ScreenTranslator.Configuration;

namespace ScreenTranslator.Translation;

internal sealed class DeepLTranslationService(DeepLClient client, IOptions<TranslationOptions> options) : ITranslationService
{
    private readonly DeepLClient _client = client;
    private readonly TranslationOptions _options = options.Value;

    public async Task<string> TranslateAsync(string text, CancellationToken cancellationToken = default)
    {
        var result = await _client.TranslateTextAsync(text, _options.SourceLang, _options.TargetLang, cancellationToken: cancellationToken);
        return result.Text;
    }
}
