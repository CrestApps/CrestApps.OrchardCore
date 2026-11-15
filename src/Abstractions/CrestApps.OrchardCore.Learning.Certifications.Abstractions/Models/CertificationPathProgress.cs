using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Certifications.Models;

/// <summary>
/// One person on one certification path: where they stand, their current certificate and every cycle with its
/// evidence. Nothing of an earlier cycle is reset or deleted.
/// </summary>
public sealed class CertificationPathProgress : CatalogItem
{
    /// <summary>
    /// Gets or sets the path.
    /// </summary>
    public string PathId { get; set; }

    /// <summary>
    /// Gets or sets the Skills certification of the path.
    /// </summary>
    public string CertificationId { get; set; }

    /// <summary>
    /// Gets or sets the person.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets where the person stands.
    /// </summary>
    public CertificationPathState State { get; set; }

    /// <summary>
    /// Gets or sets the current cycle, from 1.
    /// </summary>
    public int Cycle { get; set; } = 1;

    /// <summary>
    /// Gets or sets the Skills certificate of the last cycle that earned one.
    /// </summary>
    public string CurrentRecordId { get; set; }

    /// <summary>
    /// Gets or sets the last day the current certificate is valid.
    /// </summary>
    public DateOnly? ExpiresOn { get; set; }

    /// <summary>
    /// Gets or sets the day renewal opens.
    /// </summary>
    public DateOnly? RenewalOpensOn { get; set; }

    /// <summary>
    /// Gets or sets every cycle, the current one last.
    /// </summary>
    public List<CertificationCycle> Cycles { get; set; } = [];

    /// <summary>
    /// Gets or sets who started the path: a user name, or the requirement that started it.
    /// </summary>
    public string StartedBy { get; set; }

    /// <summary>
    /// Gets or sets when the progress was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets the current cycle.
    /// </summary>
    /// <returns>The cycle, or <see langword="null"/>.</returns>
    public CertificationCycle CurrentCycle() => Cycles.LastOrDefault();
}

/// <summary>
/// One cycle of a certification path: started, completed with evidence, and the certificate it earned.
/// </summary>
public sealed class CertificationCycle
{
    /// <summary>
    /// Gets or sets the cycle number, from 1.
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the cycle renews a certificate.
    /// </summary>
    public bool Renewal { get; set; }

    /// <summary>
    /// Gets or sets when the cycle started; evidence of a renewal counts from then.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the requirements were met.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the Skills certificate the cycle earned.
    /// </summary>
    public string RecordId { get; set; }

    /// <summary>
    /// Gets or sets the enrollments this cycle created for renewal.
    /// </summary>
    public List<string> EnrollmentIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the evidence of each requirement met.
    /// </summary>
    public List<CertificationEvidence> Evidence { get; set; } = [];
}

/// <summary>
/// What met a requirement: an enrollment completed or an attempt passed.
/// </summary>
public sealed class CertificationEvidence
{
    /// <summary>
    /// Gets or sets the requirement.
    /// </summary>
    public string RequirementId { get; set; }

    /// <summary>
    /// Gets or sets the requirement kind.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the course or quiz.
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Gets or sets the enrollment or attempt.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets a short description, such as the course title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets when it was met.
    /// </summary>
    public DateTime CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the grade or percent.
    /// </summary>
    public decimal? Grade { get; set; }
}
