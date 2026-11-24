namespace CrestApps.OrchardCore.Learning.Certifications.Models;

/// <summary>
/// How the requirements of a path combine.
/// </summary>
public enum RequirementJoin
{
    /// <summary>
    /// Every requirement is needed.
    /// </summary>
    All,

    /// <summary>
    /// Any one requirement is enough.
    /// </summary>
    Any,
}

/// <summary>
/// The date a certificate's expiry is worked out from.
/// </summary>
public enum CertificationReferenceDate
{
    /// <summary>
    /// The day the requirements were met: a renewal starts a new period from that day.
    /// </summary>
    CompletionDate,

    /// <summary>
    /// The previous expiry: a renewal keeps the anniversary, however early it was done.
    /// </summary>
    ExpiryDate,

    /// <summary>
    /// A fixed day of the year: every certificate expires on the same day.
    /// </summary>
    FixedDate,
}

/// <summary>
/// Where a person stands on a certification path.
/// </summary>
public enum CertificationPathState
{
    /// <summary>
    /// Working towards the first certificate.
    /// </summary>
    InProgress,

    /// <summary>
    /// Holding a valid certificate, before its renewal opens.
    /// </summary>
    Certified,

    /// <summary>
    /// Holding a certificate whose renewal is open.
    /// </summary>
    RenewalOpen,

    /// <summary>
    /// The certificate expired without a renewal.
    /// </summary>
    Lapsed,

    /// <summary>
    /// The certificate was revoked.
    /// </summary>
    Revoked,
}
