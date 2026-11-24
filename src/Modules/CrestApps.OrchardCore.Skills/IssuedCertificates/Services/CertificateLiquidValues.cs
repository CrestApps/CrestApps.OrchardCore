namespace CrestApps.OrchardCore.Skills.IssuedCertificates.Services;

/// <summary>
/// What a certificate template reads as <c>Certificate</c>.
/// </summary>
public sealed class CertificateLiquidValues
{
    /// <summary>
    /// Gets or sets the holder's name.
    /// </summary>
    public string HolderName { get; set; }

    /// <summary>
    /// Gets or sets the certificate number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the issue date, written out in the site's culture.
    /// </summary>
    public string IssuedOn { get; set; }

    /// <summary>
    /// Gets or sets the expiry date, written out, or <see langword="null"/> when it never expires.
    /// </summary>
    public string ExpiresOn { get; set; }

    /// <summary>
    /// Gets or sets the summary of the evidence.
    /// </summary>
    public string Evidence { get; set; }

    /// <summary>
    /// Gets or sets the verification code, in groups of four.
    /// </summary>
    public string VerificationCode { get; set; }

    /// <summary>
    /// Gets or sets the address of the public verification page.
    /// </summary>
    public string VerificationUrl { get; set; }
}

/// <summary>
/// What a certificate template reads as <c>Certification</c>.
/// </summary>
public sealed class CertificationLiquidValues
{
    /// <summary>
    /// Gets or sets the certification name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets who issues it: the certification's issuer, or the site name.
    /// </summary>
    public string Issuer { get; set; }

    /// <summary>
    /// Gets or sets its description.
    /// </summary>
    public string Description { get; set; }
}
