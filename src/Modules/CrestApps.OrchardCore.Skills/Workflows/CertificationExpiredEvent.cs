using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Skills.Workflows;

/// <summary>
/// Starts when a certificate passes its expiry date and is marked expired.
/// </summary>
public sealed class CertificationExpiredEvent : SkillsEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CertificationExpiredEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CertificationExpiredEvent(IStringLocalizer<CertificationExpiredEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CertificationExpiredEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Certificate Expired"];
}
