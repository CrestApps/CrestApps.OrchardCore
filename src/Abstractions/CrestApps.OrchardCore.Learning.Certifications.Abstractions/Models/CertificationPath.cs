using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Certifications.Models;

/// <summary>
/// How a Skills certification is earned and renewed through learning: one path per certification.
/// </summary>
public sealed class CertificationPath : CatalogItem
{
    /// <summary>
    /// Gets or sets the Skills certification the path earns.
    /// </summary>
    public string CertificationId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the path issues certificates.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether every requirement is needed, or any one.
    /// </summary>
    public RequirementJoin Join { get; set; }

    /// <summary>
    /// Gets or sets what earns the certification the first time.
    /// </summary>
    public List<CertificationRequirement> InitialRequirements { get; set; } = [];

    /// <summary>
    /// Gets or sets what renews it; empty means the initial requirements.
    /// </summary>
    public List<CertificationRequirement> RenewalRequirements { get; set; } = [];

    /// <summary>
    /// Gets or sets the date a certificate's expiry is worked out from.
    /// </summary>
    public CertificationReferenceDate ReferenceDate { get; set; }

    /// <summary>
    /// Gets or sets the month every certificate expires in, for <see cref="CertificationReferenceDate.FixedDate"/>.
    /// </summary>
    public int FixedMonth { get; set; } = 12;

    /// <summary>
    /// Gets or sets the day of the month every certificate expires on, for the fixed date rule.
    /// </summary>
    public int FixedDay { get; set; } = 31;

    /// <summary>
    /// Gets or sets the shortest first cycle, in months, for the fixed date rule.
    /// </summary>
    public int MinimumIntervalMonths { get; set; }

    /// <summary>
    /// Gets or sets how many days before expiry renewal opens.
    /// </summary>
    public int RenewalWindowDays { get; set; } = 60;

    /// <summary>
    /// Gets or sets how many days after expiry the renewal requirements still renew the certificate.
    /// </summary>
    public int GracePeriodDays { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a person starts the path by meeting a requirement, without being
    /// started by hand.
    /// </summary>
    public bool StartsOnFirstRequirement { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether renewal enrolls the holder in the renewal courses again.
    /// </summary>
    public bool EnrollOnRenewal { get; set; } = true;

    /// <summary>
    /// Gets or sets when the path was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the path was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets the requirements of a cycle: the initial ones for the first, the renewal ones (or the initial ones) after.
    /// </summary>
    /// <param name="renewal">Whether the cycle renews a certificate.</param>
    /// <returns>The requirements.</returns>
    public IReadOnlyList<CertificationRequirement> RequirementsFor(bool renewal)
    {
        return renewal && RenewalRequirements.Count > 0 ? RenewalRequirements : InitialRequirements;
    }
}
