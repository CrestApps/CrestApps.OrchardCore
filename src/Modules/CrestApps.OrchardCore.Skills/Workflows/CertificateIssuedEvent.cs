using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Skills.Workflows;

/// <summary>
/// Starts when the site issues a certificate.
/// </summary>
public sealed class CertificateIssuedEvent : SkillsEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CertificateIssuedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CertificateIssuedEvent(IStringLocalizer<CertificateIssuedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name => nameof(CertificateIssuedEvent);

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Certificate Issued"];
}
