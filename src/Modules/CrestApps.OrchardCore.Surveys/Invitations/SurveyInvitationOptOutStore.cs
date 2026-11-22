using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Surveys.Core.Access;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Invitations;

/// <summary>
/// Reads and changes the addresses that unsubscribed from survey invitations. Addresses are kept as HMACs keyed by a
/// site key protected with ASP.NET Data Protection; plain SHA-256 hashes stored before the key existed are still
/// honored. The document is saved with a concurrency check, so two unsubscribes at once never lose one of them: the
/// later commit fails instead of overwriting the list. When the key can't be read (missing Data Protection keys), every
/// address counts as unsubscribed, so no e-mail goes to someone who opted out.
/// </summary>
internal sealed class SurveyInvitationOptOutStore
{
    /// <summary>
    /// The Data Protection purpose of the key.
    /// </summary>
    public const string Purpose = "CrestApps.OrchardCore.Surveys.InvitationOptOutKey.v1";

    private const string KeyedPrefix = "h1:";

    private readonly ISession _session;
    private readonly IDataProtector _protector;
    private readonly ILogger _logger;

    private SurveyInvitationOptOutDocument _document;
    private byte[] _key;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationOptOutStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="dataProtectionProvider">The Data Protection provider, which protects the HMAC key.</param>
    /// <param name="logger">The logger.</param>
    public SurveyInvitationOptOutStore(
        ISession session,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<SurveyInvitationOptOutStore> logger)
    {
        _session = session;
        _protector = dataProtectionProvider.CreateProtector(Purpose);
        _logger = logger;
    }

    /// <summary>
    /// Gets the plain SHA-256 hash addresses were kept under before the list was keyed.
    /// </summary>
    /// <param name="address">The e-mail address.</param>
    /// <returns>The hash, or <see langword="null"/> when there is no address.</returns>
    public static string HashOf(string address)
        => string.IsNullOrWhiteSpace(address) ? null : SurveyRespondentKeys.Hash(Normalize(address));

    /// <summary>
    /// Gets the keyed hash an address is kept under.
    /// </summary>
    /// <param name="key">The HMAC key.</param>
    /// <param name="address">The e-mail address.</param>
    /// <returns>The keyed hash, or <see langword="null"/> when there is no address.</returns>
    public static string KeyedHashOf(byte[] key, string address)
    {
        ArgumentNullException.ThrowIfNull(key);

        return string.IsNullOrWhiteSpace(address)
            ? null
            : KeyedPrefix + Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(Normalize(address)))).ToLowerInvariant();
    }

    /// <summary>
    /// Gets whether an address unsubscribed.
    /// </summary>
    /// <param name="address">The e-mail address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when it unsubscribed, or when the list's key can't be read.</returns>
    public async Task<bool> ContainsAsync(string address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        var document = await LoadAsync(cancellationToken);

        if (document.AddressHashes.Contains(HashOf(address)))
        {
            return true;
        }

        if (document.ProtectedKey is null)
        {
            return false;
        }

        // Fail closed: without the key, nobody can be shown not to have unsubscribed.
        return Key(document) is not { } key || document.AddressHashes.Contains(KeyedHashOf(key, address));
    }

    /// <summary>
    /// Adds or removes an address.
    /// </summary>
    /// <param name="address">The e-mail address.</param>
    /// <param name="optedOut">Whether the address opted out.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SetAsync(string address, bool optedOut, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        var document = await LoadAsync(cancellationToken);
        var changed = false;

        if (document.ProtectedKey is null)
        {
            document.ProtectedKey = _protector.Protect(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            _key = null;
            changed = true;
        }

        var key = Key(document);
        var legacy = HashOf(address);

        if (optedOut)
        {
            // Without the key the address is kept under its plain hash, so the opt-out is still honored.
            changed |= key is null ? document.AddressHashes.Add(legacy) : document.AddressHashes.Add(KeyedHashOf(key, address)) | document.AddressHashes.Remove(legacy);
        }
        else
        {
            changed |= document.AddressHashes.Remove(legacy);

            if (key is not null)
            {
                changed |= document.AddressHashes.Remove(KeyedHashOf(key, address));
            }
        }

        if (changed)
        {
            await _session.SaveAsync(document, checkConcurrency: true, cancellationToken: cancellationToken);
        }
    }

    private static string Normalize(string address)
        => address.Trim().ToLowerInvariant();

    private byte[] Key(SurveyInvitationOptOutDocument document)
    {
        if (_key is not null)
        {
            return _key;
        }

        try
        {
            return _key = Convert.FromBase64String(_protector.Unprotect(document.ProtectedKey));
        }
        catch (CryptographicException exception)
        {
            _logger.LogError(exception, "The key of the survey invitation opt-out list could not be read; every e-mail address is treated as unsubscribed until the Data Protection keys are restored.");

            return null;
        }
    }

    private async Task<SurveyInvitationOptOutDocument> LoadAsync(CancellationToken cancellationToken)
        => _document ??= await _session.Query<SurveyInvitationOptOutDocument>().FirstOrDefaultAsync(cancellationToken) ?? new SurveyInvitationOptOutDocument();
}
