using CrestApps.OrchardCore.Learning.Certifications.Models;

namespace CrestApps.OrchardCore.Learning.Certifications.Services;

/// <summary>
/// Holds the path the certification editor posted until the certification itself is saved, so a certification that
/// fails to save never leaves a path behind.
/// </summary>
public sealed class PendingCertificationPath
{
    /// <summary>
    /// Gets or sets the path to save with its certification.
    /// </summary>
    public CertificationPath Path { get; set; }
}
