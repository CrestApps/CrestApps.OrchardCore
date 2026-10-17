using System.Net;
using System.Net.Http.Headers;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Telnyx implementation of <see cref="IVoiceMediaProvisioner"/> backed by Telnyx Media Storage. It uploads the audio
/// directly (multipart) so Telnyx hosts and plays the file by <c>media_name</c>, which means the platform never has
/// to expose a publicly reachable URL of its own for greetings, hold music, or IVR prompts.
/// </summary>
public sealed class TelnyxVoiceMediaProvisioner : IVoiceMediaProvisioner
{
    // Keep uploaded media effectively permanent: a greeting or hold-music clip is long-lived configuration, not a
    // transient recording, and the default 2-day TTL would silently delete it. Telnyx requires ttl_secs to be
    // strictly less than 630720000 (20 years), so use one second under that ceiling.
    private const string MaxTtlSeconds = "630719999";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TelnyxOptions _options;
    private readonly ILogger<TelnyxVoiceMediaProvisioner> _logger;

    public TelnyxVoiceMediaProvisioner(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<TelnyxOptions> options,
        ILogger<TelnyxVoiceMediaProvisioner> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.CurrentValue;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string ProviderTechnicalName
        => TelnyxConstants.ProviderTechnicalName;

    /// <inheritdoc/>
    public async Task<string> UploadAsync(Stream audio, string contentType, string namePrefix, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audio);

        if (!_options.IsConfigured)
        {
            return null;
        }

        // A fresh unique name per upload means a re-uploaded clip never collides with the previous one; the caller
        // deletes the old reference after this succeeds. The prefix keeps stored media identifiable by purpose.
        var prefix = string.IsNullOrWhiteSpace(namePrefix) ? "cc-voice-media" : namePrefix.Trim();
        var mediaName = $"{prefix}-{Guid.NewGuid():N}";

        try
        {
            using var client = CreateClient();
            using var form = new MultipartFormDataContent();

            var mediaContent = new StreamContent(audio);
            mediaContent.Headers.ContentType = MediaTypeHeaderValue.Parse(
                string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

            form.Add(mediaContent, "media", "media");
            form.Add(new StringContent(mediaName), "media_name");
            form.Add(new StringContent(MaxTtlSeconds), "ttl_secs");

            using var response = await client.PostAsync("media", form, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var payload = await SafeReadAsync(response, cancellationToken);

                _logger.LogError(
                    "Telnyx rejected the voice media upload with status code {StatusCode}. Response: {Response}",
                    response.StatusCode,
                    payload.SanitizeLogValue());

                return null;
            }

            return mediaName;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while uploading voice media to Telnyx Media Storage.");

            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<VoiceMediaAvailability> GetAvailabilityAsync(string mediaReference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mediaReference))
        {
            return VoiceMediaAvailability.Missing;
        }

        if (!_options.IsConfigured)
        {
            return VoiceMediaAvailability.Unknown;
        }

        try
        {
            using var client = CreateClient();
            using var response = await client.GetAsync($"media/{Uri.EscapeDataString(mediaReference)}", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return VoiceMediaAvailability.Available;
            }

            // Telnyx answers 404 for a media_name it does not hold, whether it was deleted, expired, or belongs to
            // another account. Anything else says nothing about the clip, so the caller keeps its reference.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return VoiceMediaAvailability.Missing;
            }

            _logger.LogWarning(
                "Telnyx returned {StatusCode} when asked whether it holds the voice media {MediaName}.",
                response.StatusCode,
                mediaReference.SanitizeLogValue());

            return VoiceMediaAvailability.Unknown;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An error occurred while asking Telnyx whether it holds the voice media {MediaName}.", mediaReference.SanitizeLogValue());

            return VoiceMediaAvailability.Unknown;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string mediaReference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mediaReference) || !_options.IsConfigured)
        {
            return;
        }

        try
        {
            using var client = CreateClient();
            using var response = await client.DeleteAsync($"media/{Uri.EscapeDataString(mediaReference)}", cancellationToken);

            // Deletion is idempotent: media that is already absent is a confirmed delete.
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    "Telnyx returned {StatusCode} deleting the voice media {MediaName}.",
                    response.StatusCode,
                    mediaReference.SanitizeLogValue());
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "An error occurred while deleting the voice media {MediaName} from Telnyx.", mediaReference.SanitizeLogValue());
        }
    }

    private static async Task<string> SafeReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return string.Empty;
        }
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(TelnyxConstants.ProviderTechnicalName);
        client.BaseAddress = new Uri(_options.ApiBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        return client;
    }
}
