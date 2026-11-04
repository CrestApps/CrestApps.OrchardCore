using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.Skills.Services;

/// <summary>
/// Which certification records to list.
/// </summary>
public sealed class CertificationRecordQuery
{
    /// <summary>
    /// Gets or sets a text the person's name or the certificate number must contain.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the certification, or <see langword="null"/> for every one.
    /// </summary>
    public string CertificationId { get; set; }

    /// <summary>
    /// Gets or sets the state, or <see langword="null"/> for every state.
    /// </summary>
    public CertificationStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets a day the certificates must expire on or before, for an "expiring soon" list.
    /// </summary>
    public DateOnly? ExpiresOnOrBefore { get; set; }

    /// <summary>
    /// Gets or sets the person, or an empty subject for everyone.
    /// </summary>
    public SkillSubject Subject { get; set; }
}
