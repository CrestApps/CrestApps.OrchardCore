using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.Skills.IssuedCertificates.ViewModels;

public class CertificateVerificationViewModel
{
    public string Code { get; set; }

    public bool Found { get; set; }

    public bool NotFound { get; set; }

    public string HolderName { get; set; }

    public string CertificationName { get; set; }

    public string Issuer { get; set; }

    public string Number { get; set; }

    public DateOnly? IssuedOn { get; set; }

    public DateOnly? ExpiresOn { get; set; }

    public DateOnly? RevokedOn { get; set; }

    public bool Renewed { get; set; }

    public CertificateVerificationState State { get; set; }

    public static CertificateVerificationState GetState(CertificationRecord record, DateOnly today)
    {
        if (record.Status == CertificationStatus.Revoked)
        {
            return CertificateVerificationState.Revoked;
        }

        if (record.Status == CertificationStatus.Expired || (record.ExpiresOn is { } expiresOn && expiresOn < today))
        {
            return CertificateVerificationState.Expired;
        }

        if (record.IssuedOn is { } issuedOn && issuedOn > today)
        {
            return CertificateVerificationState.NotYetValid;
        }

        return record.Status == CertificationStatus.Valid ? CertificateVerificationState.Valid : CertificateVerificationState.NotYetValid;
    }
}

public enum CertificateVerificationState
{
    Valid,
    Expired,
    Revoked,
    NotYetValid,
}
