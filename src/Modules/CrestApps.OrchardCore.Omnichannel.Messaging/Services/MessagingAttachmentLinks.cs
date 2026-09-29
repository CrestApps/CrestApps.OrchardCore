using System.Security.Cryptography;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Issues and reads the two kinds of link a stored picture is reached by. The <em>public</em> link is what a provider
/// downloads an outbound picture from: it needs no sign-in, so it is signed, expires, and names only the picture. The
/// <em>view</em> link is what the workspace shows a picture with: it is bound to its conversation, and the action
/// behind it checks the viewer may read that conversation before serving anything.
/// </summary>
public sealed class MessagingAttachmentLinks : IMessagingAttachmentUrlProvider
{
    /// <summary>
    /// The tenant-relative path of the public picture endpoint.
    /// </summary>
    public const string PublicPathPrefix = "messaging/attachments";

    private const string PublicPurpose = "CrestApps.Omnichannel.Messaging.Attachments.Public";
    private const string ViewPurpose = "CrestApps.Omnichannel.Messaging.Attachments.View";
    private const char Separator = '\n';

    private readonly ITimeLimitedDataProtector _publicProtector;
    private readonly IDataProtector _viewProtector;
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly MessagingWorkspaceOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingAttachmentLinks"/> class.
    /// </summary>
    public MessagingAttachmentLinks(
        IDataProtectionProvider dataProtectionProvider,
        ISiteService siteService,
        IHttpContextAccessor httpContextAccessor,
        IOptions<MessagingWorkspaceOptions> options,
        ILogger<MessagingAttachmentLinks> logger)
    {
        _publicProtector = dataProtectionProvider.CreateProtector(PublicPurpose).ToTimeLimitedDataProtector();
        _viewProtector = dataProtectionProvider.CreateProtector(ViewPurpose);
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> GetPublicUrlAsync(MessagingAttachment attachment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        // The site's configured address first, because a background retry has no request to read one from, and
        // behind a proxy the request's own host is the internal hop.
        var baseUrl = (await _siteService.GetSiteSettingsAsync())?.BaseUrl;
        var source = "the site base URL";

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            source = "the current request";
            var request = _httpContextAccessor.HttpContext?.Request;

            baseUrl = request is not null && request.Host.HasValue
                ? $"{request.Scheme}://{request.Host}{request.PathBase}"
                : null;
        }

        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl.TrimEnd('/'), UriKind.Absolute, out var root))
        {
            _logger.LogWarning("A picture link could not be built because the site has no base URL and there is no request to take one from.");

            return null;
        }

        // The provider downloads the picture from this host, so a host it cannot reach (localhost, an internal name)
        // is the first thing to check when a picture message fails to deliver.
        if (_logger.IsEnabled(LogLevel.Information))
        {
            // The host can come from the request, so it is sanitized like any other request value.
            _logger.LogInformation("Built a public picture link on {Host}, taken from {Source}.", root.Authority.SanitizeLogValue(), source);
        }

        var lifetime = TimeSpan.FromHours(Math.Max(1, _options.AttachmentLinkLifetimeHours));
        var token = _publicProtector.Protect(attachment.Id + Separator + attachment.ContentType, lifetime);

        // The file name only helps a provider that judges a picture by its extension; the endpoint ignores it.
        return $"{root.AbsoluteUri.TrimEnd('/')}/{PublicPathPrefix}/{token}/{PublicFileName(attachment)}";
    }

    // A provider may judge a file by its extension, so the link ends in the format's own; the endpoint ignores it.
    private static string PublicFileName(MessagingAttachment attachment)
    {
        var format = MessagingFileFormats.FindByContentType(attachment.ContentType);
        var extension = format?.PreferredExtension ?? string.Empty;

        return (format?.IsImage == true ? "image" : "file") + extension;
    }

    /// <summary>
    /// Reads a public link's token.
    /// </summary>
    /// <param name="token">The token from the link.</param>
    /// <param name="attachmentId">The stored picture it names.</param>
    /// <param name="contentType">The picture's media type.</param>
    /// <returns><see langword="true"/> when the token is genuine and has not expired.</returns>
    public bool TryReadPublicToken(string token, out string attachmentId, out string contentType)
    {
        attachmentId = null;
        contentType = null;

        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        try
        {
            var parts = _publicProtector.Unprotect(token).Split(Separator);

            if (parts.Length != 2)
            {
                return false;
            }

            (attachmentId, contentType) = (parts[0], parts[1]);

            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>
    /// Issues the token a file is shown or downloaded in a conversation with.
    /// </summary>
    /// <param name="conversationId">The conversation the file belongs to.</param>
    /// <param name="attachment">The stored file.</param>
    /// <returns>The token.</returns>
    public string CreateViewToken(string conversationId, MessagingAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        // The name is only ever used as the download name, and the separator cannot appear in it.
        var fileName = (attachment.FileName ?? string.Empty).Replace(Separator, ' ');

        return _viewProtector.Protect(conversationId + Separator + attachment.Id + Separator + attachment.ContentType + Separator + fileName);
    }

    /// <summary>
    /// Reads a view token, and confirms it was issued for the conversation it is being used with.
    /// </summary>
    /// <param name="conversationId">The conversation the request names.</param>
    /// <param name="token">The token from the request.</param>
    /// <param name="attachmentId">The stored file it names.</param>
    /// <param name="contentType">The file's media type.</param>
    /// <param name="fileName">The file's name as the agent attached it, when it had one.</param>
    /// <returns><see langword="true"/> when the token is genuine and belongs to <paramref name="conversationId"/>.</returns>
    public bool TryReadViewToken(string conversationId, string token, out string attachmentId, out string contentType, out string fileName)
    {
        attachmentId = null;
        contentType = null;
        fileName = null;

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(conversationId))
        {
            return false;
        }

        try
        {
            var parts = _viewProtector.Unprotect(token).Split(Separator);

            if (parts.Length != 4 || !string.Equals(parts[0], conversationId, StringComparison.Ordinal))
            {
                return false;
            }

            (attachmentId, contentType, fileName) = (parts[1], parts[2], parts[3]);

            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
