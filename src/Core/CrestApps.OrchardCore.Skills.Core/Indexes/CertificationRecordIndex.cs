using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.Skills.Core.Indexes;

/// <summary>
/// The YesSql index of certification records.
/// </summary>
public sealed class CertificationRecordIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets what kind of record holds the certificate.
    /// </summary>
    public SkillSubjectKind SubjectKind { get; set; }

    /// <summary>
    /// Gets or sets the user id or worker id that holds the certificate.
    /// </summary>
    public string SubjectId { get; set; }

    /// <summary>
    /// Gets or sets the person's name.
    /// </summary>
    public string SubjectName { get; set; }

    /// <summary>
    /// Gets or sets the certification.
    /// </summary>
    public string CertificationId { get; set; }

    /// <summary>
    /// Gets or sets the certificate number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the record's state.
    /// </summary>
    public CertificationStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the issue day as <c>yyyyMMdd</c>, or <see langword="null"/>.
    /// </summary>
    public int? IssuedOn { get; set; }

    /// <summary>
    /// Gets or sets the expiry day as <c>yyyyMMdd</c>, or <see langword="null"/> when it never expires.
    /// </summary>
    public int? ExpiresOn { get; set; }
}
