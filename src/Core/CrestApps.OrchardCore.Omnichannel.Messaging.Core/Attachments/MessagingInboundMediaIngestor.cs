using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;

/// <summary>
/// The default <see cref="IMessagingInboundMediaIngestor"/>. It downloads each announced media item over HTTPS,
/// keeps it only when it is one of the formats the message's channel carries and no larger than the configured limit,
/// and stores it under a key derived from the provider's message id, so a redelivered message overwrites its own files
/// rather than
/// storing them again.
/// </summary>
public sealed class MessagingInboundMediaIngestor : IMessagingInboundMediaIngestor
{
    /// <summary>
    /// The name of the HTTP client inbound media is downloaded with.
    /// </summary>
    public const string HttpClientName = "CrestApps.Omnichannel.Messaging.InboundMedia";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMessagingAttachmentStore _attachmentStore;
    private readonly IMessagingChannelResolver _channelResolver;
    private readonly IEnumerable<IMessagingMediaRequestAuthenticator> _authenticators;
    private readonly MessagingWorkspaceOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingInboundMediaIngestor"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The factory of the client media is downloaded with.</param>
    /// <param name="attachmentStore">The store the files are kept in.</param>
    /// <param name="channelResolver">The resolver of the channel whose formats decide what is kept.</param>
    /// <param name="authenticators">The providers' signers for media they serve only to their own account.</param>
    /// <param name="options">The workspace options carrying the size and count limits.</param>
    /// <param name="logger">The logger.</param>
    public MessagingInboundMediaIngestor(
        IHttpClientFactory httpClientFactory,
        IMessagingAttachmentStore attachmentStore,
        IMessagingChannelResolver channelResolver,
        IEnumerable<IMessagingMediaRequestAuthenticator> authenticators,
        IOptions<MessagingWorkspaceOptions> options,
        ILogger<MessagingInboundMediaIngestor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _attachmentStore = attachmentStore;
        _channelResolver = channelResolver;
        _authenticators = authenticators ?? [];
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> IngestAsync(OmnichannelMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!message.IsInbound || message.MediaReferences is null || message.MediaReferences.Count == 0)
        {
            return false;
        }

        var part = message.GetOrCreate<MessagingMessageAttachments>();

        if (part.Ingested)
        {
            return false;
        }

        var sources = message.MediaReferences
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .ToArray();

        var formats = _channelResolver.Get(message.Channel)?.Capabilities.Attachments?.Formats ?? [];
        var maxCount = Math.Max(1, _options.MaxInboundAttachments);
        var stored = new List<MessagingAttachment>();
        var skipped = Math.Max(0, sources.Length - maxCount);

        for (var index = 0; index < Math.Min(sources.Length, maxCount); index++)
        {
            var attachment = await TryIngestAsync(message, sources[index], index, formats, cancellationToken);

            if (attachment is null)
            {
                skipped++;
            }
            else
            {
                stored.Add(attachment);
            }
        }

        part.Items = stored;
        part.SkippedCount = skipped;
        part.Ingested = true;
        message.Put(part);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Stored {Stored} of {Announced} media item(s) received on {Channel} message {ProviderMessageId}.",
                stored.Count,
                sources.Length,
                message.Channel,
                message.ProviderMessageId.SanitizeLogValue());
        }

        return true;
    }

    private async Task<MessagingAttachment> TryIngestAsync(OmnichannelMessage message, string source, int index, IReadOnlyList<MessagingFileFormat> formats, CancellationToken cancellationToken)
    {
        if (formats.Count == 0)
        {
            _logger.LogWarning(
                "Skipped media item {Index} of {Channel} message {ProviderMessageId} because the channel carries no attachments.",
                index,
                message.Channel,
                message.ProviderMessageId.SanitizeLogValue());

            return null;
        }

        // The address comes from a signed provider webhook, but it is still only ever fetched over HTTPS, so a
        // payload naming a plain-text address is never followed.
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            _logger.LogWarning(
                "Skipped media item {Index} of {Channel} message {ProviderMessageId} because its address is not an absolute HTTPS URL.",
                index,
                message.Channel,
                message.ProviderMessageId.SanitizeLogValue());

            return null;
        }

        var maxBytes = Math.Max(1, _options.MaxInboundAttachmentBytes);

        try
        {
            using var client = _httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

            foreach (var authenticator in _authenticators)
            {
                if (await authenticator.TryAuthenticateAsync(request, cancellationToken))
                {
                    break;
                }
            }

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Skipped media item {Index} of {Channel} message {ProviderMessageId}: the provider answered {StatusCode}.",
                    index,
                    message.Channel,
                    message.ProviderMessageId.SanitizeLogValue(),
                    (int)response.StatusCode);

                return null;
            }

            if (response.Content.Headers.ContentLength is long declared && declared > maxBytes)
            {
                LogTooLarge(message, index, declared.ToString(CultureInfo.InvariantCulture), maxBytes);

                return null;
            }

            var bytes = await ReadBoundedAsync(response.Content, maxBytes, cancellationToken);

            if (bytes is null)
            {
                LogTooLarge(message, index, "more than the limit", maxBytes);

                return null;
            }

            // The last segment of the address stands in for a file name: some formats are only known by their extension.
            var fileName = Path.GetFileName(uri.AbsolutePath);
            var declaredType = response.Content.Headers.ContentType?.MediaType;
            var format = MessagingFileFormats.Detect(bytes, fileName, declaredType, formats);

            if (format is null)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "Skipped media item {Index} of {Channel} message {ProviderMessageId}: the channel does not carry it (declared as {DeclaredType}).",
                        index,
                        message.Channel,
                        message.ProviderMessageId.SanitizeLogValue(),
                        declaredType.SanitizeLogValue());
                }

                return null;
            }

            var attachment = new MessagingAttachment
            {
                Id = CreateAttachmentId(message, index),
                ContentType = format.ContentType,
                FileName = format.MatchesExtension(fileName) ? fileName : null,
                Length = bytes.Length,
            };

            await _attachmentStore.StoreAsync(attachment.Id, bytes, cancellationToken);

            return attachment;
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(
                ex,
                "Skipped media item {Index} of {Channel} message {ProviderMessageId} because it could not be downloaded.",
                index,
                message.Channel,
                message.ProviderMessageId.SanitizeLogValue());

            return null;
        }
    }

    private void LogTooLarge(OmnichannelMessage message, int index, string length, long maxBytes)
        => _logger.LogWarning(
            "Skipped media item {Index} of {Channel} message {ProviderMessageId}: {Length} bytes is over the {MaxBytes} byte limit.",
            index,
            message.Channel,
            message.ProviderMessageId.SanitizeLogValue(),
            length,
            maxBytes);

    // A declared length can be missing or wrong, so the download itself is held to the limit.
    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, long maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;

        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    // The provider's message id keeps the key stable across redeliveries of the same message; a message without one
    // gets keys of its own.
    internal static string CreateAttachmentId(OmnichannelMessage message, int index)
    {
        if (string.IsNullOrEmpty(message.ProviderMessageId))
        {
            return $"{Guid.NewGuid():N}-{index}";
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{message.Channel}|{message.ProviderMessageId}|{index}"));

        return $"in-{Convert.ToHexStringLower(hash)[..40]}";
    }
}
