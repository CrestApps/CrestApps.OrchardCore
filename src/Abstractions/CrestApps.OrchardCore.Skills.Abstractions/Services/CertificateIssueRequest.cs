using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.Skills.Services;

/// <summary>
/// What <see cref="ICertificateIssuer"/> needs to issue a certificate.
/// </summary>
public sealed class CertificateIssueRequest
{
    /// <summary>
    /// Gets or sets the certification.
    /// </summary>
    public string CertificationId { get; set; }

    /// <summary>
    /// Gets or sets who earns the certificate.
    /// </summary>
    public SkillSubject Subject { get; set; }

    /// <summary>
    /// Gets or sets the holder's name as it is printed.
    /// </summary>
    public string SubjectName { get; set; }

    /// <summary>
    /// Gets or sets what earned the certificate, such as <c>Learning.CertificationPath</c>.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the id, within <see cref="Source"/>, of what earned it. Issuing again with the same certification,
    /// source and source id returns the certificate already issued.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets a short summary of the evidence.
    /// </summary>
    public string Evidence { get; set; }

    /// <summary>
    /// Gets or sets the day it is issued.
    /// </summary>
    public DateOnly IssuedOn { get; set; }

    /// <summary>
    /// Gets or sets the last day it is valid, when the issuer works it out itself (for example to keep an anniversary);
    /// <see langword="null"/> takes the validity of the certification from <see cref="IssuedOn"/>.
    /// </summary>
    public DateOnly? ExpiresOn { get; set; }

    /// <summary>
    /// Gets or sets who issues it, such as a user name or the name of an automatic process.
    /// </summary>
    public string Actor { get; set; }
}
