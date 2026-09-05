using CrestApps.OrchardCore.ContactCenter.Services;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Plays a phone menu to a caller on a Telnyx leg and collects the key they press. The key arrives back as a
/// <c>call.gather.ended</c> webhook, which the Contact Center inbound path applies to the flow.
/// </summary>
public sealed class TelnyxIvrProvider : IIvrProvider
{
    private readonly TelnyxApiClient _apiClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxIvrProvider"/> class.
    /// </summary>
    /// <param name="apiClient">The typed Telnyx client.</param>
    public TelnyxIvrProvider(TelnyxApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <inheritdoc/>
    public async Task<bool> PromptAsync(
        string providerCallId,
        string text,
        string mediaId,
        string validDigits,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerCallId))
        {
            return false;
        }

        var body = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            // One key per menu level. A menu that waits for several digits leaves the caller wondering whether it
            // heard them, and every option here is a single key.
            ["maximum_digits"] = 1,
        };

        if (!string.IsNullOrWhiteSpace(validDigits))
        {
            body["valid_digits"] = validDigits;
        }

        // Recorded audio wins over synthesized speech when the menu has it: a tenant who recorded their menu did
        // so because they did not want it read out.
        if (!string.IsNullOrWhiteSpace(mediaId))
        {
            body["audio_url"] = mediaId;

            var played = await _apiClient.PostCallActionAsync(providerCallId, "gather_using_audio", body, cancellationToken);

            return played.Succeeded;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var result = await _apiClient.GatherAsync(providerCallId, text, validDigits, cancellationToken: cancellationToken);

        return result.Succeeded;
    }
}
