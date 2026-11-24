using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.Skills.Services;

/// <summary>
/// The result of issuing or renewing a certificate.
/// </summary>
public sealed class CertificateIssueResult
{
    private readonly List<string> _errors = [];

    /// <summary>
    /// Gets the reasons the certificate was not issued.
    /// </summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>
    /// Gets a value indicating whether the certificate was issued, now or before.
    /// </summary>
    public bool Succeeded => _errors.Count == 0 && Record is not null;

    /// <summary>
    /// Gets or sets the certificate.
    /// </summary>
    public CertificationRecord Record { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the certificate had been issued before, so nothing new was created.
    /// </summary>
    public bool AlreadyIssued { get; set; }

    /// <summary>
    /// Records why the certificate was not issued.
    /// </summary>
    /// <param name="error">The reason.</param>
    public void Fail(string error)
    {
        ArgumentException.ThrowIfNullOrEmpty(error);

        _errors.Add(error);
    }
}
