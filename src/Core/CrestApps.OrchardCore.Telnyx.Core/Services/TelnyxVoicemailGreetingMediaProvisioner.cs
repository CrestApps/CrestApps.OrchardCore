using System.Net;
using System.Net.Http.Headers;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Telnyx implementation of <see cref="IVoicemailGreetingMediaProvisioner"/> backed by Telnyx Media Storage. It
/// uploads the greeting audio directly (multipart) so Telnyx hosts and plays the file by <c>media_name</c>, which
/// means the platform never has to expose a publicly reachable greeting URL of its own.
/// </summary>
public sealed class TelnyxVoicemailGreetingMediaProvisioner : IVoicemailGreetingMediaProvisioner
{
    // Keep the greeting for the maximum Telnyx retention (20 years) so it is effectively permanent: a greeting is
    // long-lived configuration, not a transient recording, and the default 2-day TTL would silently delete it.
    private const string MaxTtlSeconds = "630720000";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TelnyxOptions _options;
    private readonly ILogger<TelnyxVoicemailGreetingMediaProvisioner> _logger;

    public TelnyxVoicemailGreetingMediaProvisioner(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<TelnyxOptions> options,
        ILogger<TelnyxVoicemailGreetingMediaProvisioner> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.CurrentValue;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> UploadAsync(Stream audio, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audio);

        if (!_options.IsConfigured)
        {
            return null;
        }

        // A fresh unique name per upload means a re-recorded greeting never collides with the previous one; the
        // caller deletes the old reference after this succeeds.
        var mediaName = $"cc-vm-greeting-{Guid.NewGuid():N}";

        try
        {
            using var client = CreateClient();
            using var form = new MultipartFormDataContent();

            var mediaContent = new StreamContent(audio);
            mediaContent.Headers.ContentType = MediaTypeHeaderValue.Parse(
                string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

            form.Add(mediaContent, "media", "greeting");
            form.Add(new StringContent(mediaName), "media_name");
            form.Add(new StringContent(MaxTtlSeconds), "ttl_secs");

            using var response = await client.PostAsync("media", form, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var payload = await SafeReadAsync(response, cancellationToken);

                _logger.LogError(
                    "Telnyx rejected the voicemail greeting upload with status code {StatusCode}. Response: {Response}",
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
            _logger.LogError(ex, "An error occurred while uploading the voicemail greeting to Telnyx Media Storage.");

            return null;
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

            // Deletion is idempotent: a greeting that is already absent is a confirmed delete.
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    "Telnyx returned {StatusCode} deleting the voicemail greeting media {MediaName}.",
                    response.StatusCode,
                    mediaReference.SanitizeLogValue());
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "An error occurred while deleting the voicemail greeting media {MediaName} from Telnyx.", mediaReference.SanitizeLogValue());
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
