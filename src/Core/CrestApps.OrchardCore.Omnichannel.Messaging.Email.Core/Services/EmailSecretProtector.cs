using CrestApps.OrchardCore.Core.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;

/// <summary>
/// The default <see cref="IEmailSecretProtector"/>, over the tenant's data protection keys through
/// <see cref="ProtectedSettingExtensions"/>, with the channel's one purpose so the secrets already stored stay readable.
/// </summary>
public sealed class EmailSecretProtector : IEmailSecretProtector
{
    private readonly IDataProtectionProvider _dataProtectionProvider;
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
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Protect(string secret)
        => string.IsNullOrEmpty(secret) ? null : _dataProtectionProvider.ProtectSetting(EmailChannelConstants.SecretProtectorPurpose, secret);

    /// <inheritdoc/>
    public string Unprotect(string protectedSecret)
        // A secret protected with keys the tenant no longer has (a copied database, a reset key ring) has to be entered
        // again; it reads as not entered, with a warning, rather than failing every send with an unexplained error.
        => _dataProtectionProvider.UnprotectSettingOrDefault(EmailChannelConstants.SecretProtectorPurpose, protectedSecret, _logger, "email channel secret");
}
