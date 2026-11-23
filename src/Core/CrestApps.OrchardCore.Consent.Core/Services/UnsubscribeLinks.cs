using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Consent.Unsubscribe;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Emails;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Consent.Core.Services;

/// <summary>
/// The default <see cref="IUnsubscribeLinks"/>: a token signed with <see cref="SignedTokenService"/> for the
/// unsubscribe purpose, which holds the normalized address and the scope, on the site's base URL followed by
/// <see cref="ConsentConstants.PreferencesPath"/>.
/// </summary>
/// <remarks>
/// The token's payload is versioned JSON (<c>v</c>, <c>a</c>, <c>k</c>, <c>i</c>), so a later version can add to it
/// while links already sent keep working. A link keeps working for <see cref="Lifetime"/>, long enough for an e-mail
/// that sits in an inbox for a year.
/// </remarks>
public sealed class UnsubscribeLinks : IUnsubscribeLinks
{
    /// <summary>
    /// The purpose the tokens are signed for. A token signed for any other purpose is not read.
    /// </summary>
    public const string TokenPurpose = "CrestApps.Consent.Unsubscribe";

    /// <summary>
    /// How long a link keeps working.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(400);

    private const int PayloadVersion = 1;
    private const string VersionKey = "v";
    private const string AddressKey = "a";
    private const string KindKey = "k";
    private const string IdKey = "i";

    private readonly SignedTokenService _signedTokenService;
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnsubscribeLinks"/> class.
    /// </summary>
    /// <param name="signedTokenService">The service that signs and reads the tokens.</param>
    /// <param name="siteService">The site service, for the site's base URL.</param>
    public UnsubscribeLinks(
        SignedTokenService signedTokenService,
        ISiteService siteService)
    {
        _signedTokenService = signedTokenService;
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public async Task<string> CreateUrlAsync(string address, UnsubscribeScope scope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope.Kind, nameof(scope));

        var normalized = EmailAddresses.Normalize(address);

        if (normalized is null)
        {
            return null;
        }

        var baseUrl = (await _siteService.GetSiteSettingsAsync())?.BaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        var payload = new JsonObject
        {
            [VersionKey] = PayloadVersion,
            [AddressKey] = normalized,
            [KindKey] = scope.Kind,
            [IdKey] = scope.Id,
        };

        var token = _signedTokenService.Protect(TokenPurpose, payload.ToJsonString(), Lifetime);

        // In the query string: IIS (HTTP.sys) refuses a path segment longer than 260 characters, and a token can be.
        return $"{baseUrl.TrimEnd('/')}/{ConsentConstants.PreferencesPath}?token={Uri.EscapeDataString(token)}";
    }

    /// <inheritdoc/>
    public bool TryRead(string token, out UnsubscribeTicket ticket)
    {
        ticket = null;

        if (_signedTokenService.Unprotect(TokenPurpose, token) is not { } value)
        {
            return false;
        }

        try
        {
            if (JsonNode.Parse(value) is not JsonObject payload ||
                payload[VersionKey]?.GetValue<int>() != PayloadVersion)
            {
                return false;
            }

            var address = EmailAddresses.Normalize(payload[AddressKey]?.GetValue<string>());
            var kind = payload[KindKey]?.GetValue<string>();

            if (address is null || string.IsNullOrWhiteSpace(kind))
            {
                return false;
            }

            ticket = new UnsubscribeTicket
            {
                Address = address,
                Scope = new UnsubscribeScope
                {
                    Kind = kind,
                    Id = payload[IdKey]?.GetValue<string>(),
                },
            };

            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }
}
