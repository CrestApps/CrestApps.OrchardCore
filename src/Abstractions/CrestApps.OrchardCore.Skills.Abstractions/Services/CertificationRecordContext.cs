using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.Skills.Services;

/// <summary>
/// What an <see cref="ICertificationRecordObserver"/> hears about.
/// </summary>
public sealed class CertificationRecordContext
{
    /// <summary>
    /// Gets or sets the certificate.
    /// </summary>
    public CertificationRecord Record { get; set; }

    /// <summary>
    /// Gets or sets its certification.
    /// </summary>
    public Certification Certification { get; set; }

    /// <summary>
    /// Gets or sets the certificate a renewal replaces.
    /// </summary>
    public CertificationRecord PreviousRecord { get; set; }

    /// <summary>
    /// Gets or sets who made the change.
    /// </summary>
    public string Actor { get; set; }
}
