namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// One of a worker's certificates, shown in the worker editor.
/// </summary>
public class WorkerCertificateViewModel
{
    /// <summary>
    /// Gets or sets the record id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the certification name.
    /// </summary>
    public string CertificationName { get; set; }

    /// <summary>
    /// Gets or sets the certificate number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the last day it is valid.
    /// </summary>
    public DateOnly? ExpiresOn { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public string Status { get; set; }
}
