using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Omnichannel.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;

/// <summary>
/// The default <see cref="IEmailUnsubscribeLinks"/>: the token is the addresses, protected with the tenant's data
/// protection keys, so it needs no storage and cannot be forged.
/// </summary>
public sealed class EmailUnsubscribeLinks : IEmailUnsubscribeLinks
{
    private const string Version = "1";
    private const char Separator = '\n';

    private readonly IDataProtector _protector;
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailUnsubscribeLinks"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The tenant's data protection provider.</param>
    /// <param name="siteService">The site service the public base URL is read from.</param>
    public EmailUnsubscribeLinks(
        IDataProtectionProvider dataProtectionProvider,
        ISiteService siteService)
    {
        _protector = dataProtectionProvider.CreateProtector(EmailChannelConstants.UnsubscribeProtectorPurpose);
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
        var token = WebEncoders.Base64UrlEncode(_protector.Protect(Encoding.UTF8.GetBytes(payload)));
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

        try
        {
            var payload = Encoding.UTF8.GetString(_protector.Unprotect(WebEncoders.Base64UrlDecode(token)));
            var parts = payload.Split(Separator);

            if (parts.Length != 3 || parts[0] != Version || string.IsNullOrEmpty(parts[1]))
            {
                return false;
            }

            contactAddress = parts[1];
            serviceAddress = string.IsNullOrEmpty(parts[2]) ? null : parts[2];

            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return false;
        }
    }
}
