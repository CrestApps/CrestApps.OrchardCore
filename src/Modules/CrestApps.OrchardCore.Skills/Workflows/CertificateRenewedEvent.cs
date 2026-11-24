using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Skills.Workflows;

/// <summary>
/// Starts when the site issues the certificate that renews another one.
/// </summary>
public sealed class CertificateRenewedEvent : SkillsEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CertificateRenewedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CertificateRenewedEvent(IStringLocalizer<CertificateRenewedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name => nameof(CertificateRenewedEvent);

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Certificate Renewed"];
}
