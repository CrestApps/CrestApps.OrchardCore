namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The form that adds a certificate to a worker.
/// </summary>
public class AddWorkerCertificateViewModel
{
    /// <summary>
    /// Gets or sets the worker id.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the certification.
    /// </summary>
    public string CertificationId { get; set; }

    /// <summary>
    /// Gets or sets the certificate number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the issue date.
    /// </summary>
    public DateOnly? IssuedOn { get; set; }

    /// <summary>
    /// Gets or sets the last valid day; empty to work it out from the certification.
    /// </summary>
    public DateOnly? ExpiresOn { get; set; }

    /// <summary>
    /// Gets or sets whether the certificate was checked.
    /// </summary>
    public bool Checked { get; set; }
}
