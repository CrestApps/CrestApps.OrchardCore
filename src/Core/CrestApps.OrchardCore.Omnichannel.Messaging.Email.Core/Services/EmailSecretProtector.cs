using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;

/// <summary>
/// The default <see cref="IEmailSecretProtector"/>, over the tenant's data protection keys.
/// </summary>
public sealed class EmailSecretProtector : IEmailSecretProtector
{
    private readonly IDataProtector _protector;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailSecretProtector"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The tenant's data protection provider.</param>
    /// <param name="logger">The logger.</param>
    public EmailSecretProtector(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<EmailSecretProtector> logger)
    {
        _protector = dataProtectionProvider.CreateProtector(EmailChannelConstants.SecretProtectorPurpose);
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Protect(string secret)
        => string.IsNullOrEmpty(secret) ? null : _protector.Protect(secret);

    /// <inheritdoc/>
    public string Unprotect(string protectedSecret)
    {
        if (string.IsNullOrEmpty(protectedSecret))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(protectedSecret);
        }
        catch (CryptographicException ex)
        {
            // The keys the secret was protected with are gone (a copied database, a reset key ring). The secret has to
            // be entered again; saying so is better than failing every send with an unexplained error.
            _logger.LogWarning(ex, "An email channel secret could not be read with the tenant's data protection keys and has to be entered again.");

            return null;
        }
    }
}
