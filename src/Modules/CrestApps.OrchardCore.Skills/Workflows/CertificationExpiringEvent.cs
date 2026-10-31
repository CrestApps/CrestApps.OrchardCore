using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Skills.Workflows;

/// <summary>
/// Starts when a certificate reaches one of its certification's reminder days before expiry.
/// </summary>
public sealed class CertificationExpiringEvent : SkillsEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CertificationExpiringEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CertificationExpiringEvent(IStringLocalizer<CertificationExpiringEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CertificationExpiringEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Certificate Expiring"];
}
