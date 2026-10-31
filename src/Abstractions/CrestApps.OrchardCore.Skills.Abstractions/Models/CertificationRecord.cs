using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Skills.Models;

/// <summary>
/// One person's certificate: which certification, when it was issued, when it expires and whether it was checked.
/// </summary>
public sealed class CertificationRecord : CatalogItem
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
    /// Gets or sets the person's name when the record was saved, so lists can show it without loading the person.
    /// </summary>
    public string SubjectName { get; set; }

    /// <summary>
    /// Gets or sets the certification.
    /// </summary>
    public string CertificationId { get; set; }

    /// <summary>
    /// Gets or sets the certificate's number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the day it was issued.
    /// </summary>
    public DateOnly? IssuedOn { get; set; }

    /// <summary>
    /// Gets or sets the last day it is valid, or <see langword="null"/> when it never expires.
    /// </summary>
    public DateOnly? ExpiresOn { get; set; }

    /// <summary>
    /// Gets or sets the media library path of a copy of the certificate.
    /// </summary>
    public string DocumentPath { get; set; }

    /// <summary>
    /// Gets or sets the record's state.
    /// </summary>
    public CertificationStatus Status { get; set; }

    /// <summary>
    /// Gets or sets who checked it.
    /// </summary>
    public string VerifiedBy { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it was checked.
    /// </summary>
    public DateTime? VerifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets a note about the record.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets the days-before-expiry reminders already raised for <see cref="RemindersExpiresOn"/>, so each is
    /// raised once.
    /// </summary>
    public IList<int> RemindersRaised { get; set; } = [];

    /// <summary>
    /// Gets or sets the expiry date <see cref="RemindersRaised"/> belongs to. When the certificate is renewed and its
    /// expiry date moves, the reminders start again.
    /// </summary>
    public DateOnly? RemindersExpiresOn { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the record was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the record was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets the subject that holds the certificate.
    /// </summary>
    public SkillSubject Subject => new(SubjectKind, SubjectId);

    /// <summary>
    /// Gets whether the certificate counts on a day: it was checked, it was issued by then and it hasn't expired.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns><see langword="true"/> when it counts.</returns>
    public bool IsValidOn(DateOnly date)
    {
        return Status == CertificationStatus.Valid
            && (IssuedOn is null || IssuedOn <= date)
            && (ExpiresOn is null || ExpiresOn >= date);
    }
}
