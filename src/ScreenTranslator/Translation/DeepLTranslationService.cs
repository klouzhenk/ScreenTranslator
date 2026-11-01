using DeepL;
using Microsoft.Extensions.Options;
using ScreenTranslator.Configuration;
using ScreenTranslator.Translation.Exceptions;
using System.Net.Http;

namespace ScreenTranslator.Translation;

internal sealed class DeepLTranslationService(DeepLClient client, IOptions<TranslationOptions> options) : ITranslationService
{
    private readonly DeepLClient _client = client;
    private readonly TranslationOptions _options = options.Value;

    public async Task<string> TranslateAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.TranslateTextAsync(text, _options.SourceLang, _options.TargetLang, cancellationToken: cancellationToken);
            return result.Text;
        }
        catch (AuthorizationException ex)
        {
            throw new TranslationException(TranslationErrorKind.Authorization, ex.Message, ex);
        }
        catch (QuotaExceededException ex)
        {
            throw new TranslationException(TranslationErrorKind.Quota, ex.Message, ex);
        }
        catch (Exception ex) when (ex is ConnectionException or HttpRequestException)
        {
            throw new TranslationException(TranslationErrorKind.Connection, ex.Message, ex);
        }
        catch (DeepLException ex)
        {
            throw new TranslationException(TranslationErrorKind.Unknown, ex.Message, ex);
        }
    }
}
