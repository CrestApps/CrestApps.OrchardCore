using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.Skills.ViewModels;

/// <summary>
/// A row of the certificate list.
/// </summary>
public class CertificationRecordRowViewModel
{
    /// <summary>
    /// Gets or sets the certificate.
    /// </summary>
    public CertificationRecord Record { get; set; }

    /// <summary>
    /// Gets or sets the name of its certification.
    /// </summary>
    public string CertificationName { get; set; }
}
