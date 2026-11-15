using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Skills.Workflows;

/// <summary>
/// Starts when a certificate is revoked.
/// </summary>
public sealed class CertificateRevokedEvent : SkillsEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CertificateRevokedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CertificateRevokedEvent(IStringLocalizer<CertificateRevokedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name => nameof(CertificateRevokedEvent);

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Certificate Revoked"];
}
