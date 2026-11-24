using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;

/// <summary>
/// The default <see cref="IEmailUnsubscribeLinks"/>: the token is the addresses, signed with
/// <see cref="SignedTokenService"/>, so it needs no storage and cannot be forged.
/// </summary>
/// <remarks>
/// A link works for <see cref="Lifetime"/>, as the marketing unsubscribe links do: far longer than the 30 days the
/// anti-spam laws ask an unsubscribe link to keep working, so an old email can still be unsubscribed from.
/// </remarks>
public sealed class EmailUnsubscribeLinks : IEmailUnsubscribeLinks
{
    /// <summary>
    /// How long an unsubscribe link keeps working.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(400);

    private const string Version = "1";
    private const char Separator = '\n';

    private readonly SignedTokenService _signedTokens;
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailUnsubscribeLinks"/> class.
    /// </summary>
    /// <param name="signedTokens">The service the tokens are signed and read with.</param>
    /// <param name="siteService">The site service the public base URL is read from.</param>
    public EmailUnsubscribeLinks(
        SignedTokenService signedTokens,
        ISiteService siteService)
    {
        _signedTokens = signedTokens;
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public async Task<string> CreateUrlAsync(string contactAddress, string serviceAddress, CancellationToken cancellationToken = default)
    {
        var contact = OmnichannelEmailAddress.Normalize(contactAddress);

        if (string.IsNullOrEmpty(contact))
        {
            return null;
        }

        // The link is opened from the recipient's mail client, so it is built on the site's public address, never on
        // whatever host a background task happens to run under.
        var site = await _siteService.GetSiteSettingsAsync();

        if (string.IsNullOrWhiteSpace(site.BaseUrl) || !Uri.TryCreate(site.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            return null;
        }

        var payload = string.Join(Separator, Version, contact, OmnichannelEmailAddress.Normalize(serviceAddress) ?? string.Empty);

        // The token is URL-safe as it comes, so it goes into the path as it is.
        var token = _signedTokens.Protect(EmailChannelConstants.UnsubscribeProtectorPurpose, payload, Lifetime);
        var route = EmailChannelConstants.UnsubscribeRoute.Replace("{token}", token, StringComparison.Ordinal);

        return $"{baseUri.AbsoluteUri.TrimEnd('/')}/{route}";
    }

    /// <inheritdoc/>
    public bool TryRead(string token, out string contactAddress, out string serviceAddress)
    {
        contactAddress = null;
        serviceAddress = null;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        string payload;

        try
        {
            payload = _signedTokens.Unprotect(EmailChannelConstants.UnsubscribeProtectorPurpose, token);
        }
        catch (FormatException)
        {
            // The token comes from the URL as anybody typed it; one that is not even Base64 is simply not a link.
            return false;
        }

        var parts = payload?.Split(Separator);

        if (parts is not { Length: 3 } || parts[0] != Version || string.IsNullOrEmpty(parts[1]))
        {
            return false;
        }

        contactAddress = parts[1];
        serviceAddress = string.IsNullOrEmpty(parts[2]) ? null : parts[2];

        return true;
    }
}
