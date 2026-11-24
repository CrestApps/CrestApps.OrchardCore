using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.Skills.Services;

/// <summary>
/// Issues certificates on behalf of the site: numbered when the certification says so, with a verification code for
/// the public page, saved and committed before the observers hear about them.
/// </summary>
/// <remarks>
/// Issuing is idempotent: one certificate per certification, source and source id, and one renewal per certificate,
/// however often the event that triggers it arrives, at the same time or later.
/// </remarks>
public interface ICertificateIssuer
{
    /// <summary>
    /// Issues a certificate, or returns the one already issued for the same certification, source and source id.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<CertificateIssueResult> IssueAsync(CertificateIssueRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues the certificate that renews another one, as a new record linked to it; returns the renewal already issued
    /// when there is one. The earlier certificate keeps its dates and history.
    /// </summary>
    /// <param name="previousRecordId">The certificate being renewed.</param>
    /// <param name="request">The request for the new certificate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<CertificateIssueResult> RenewAsync(string previousRecordId, CertificateIssueRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a certificate. The verification page shows it as revoked at once.
    /// </summary>
    /// <param name="recordId">The certificate.</param>
    /// <param name="actor">Who revokes it.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<SkillsResult> RevokeAsync(string recordId, string actor, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an issued certificate by its verification code, ignoring case and the separators people type.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The certificate, or <see langword="null"/>.</returns>
    Task<CertificationRecord> FindByVerificationCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the certificate issued for a certification, source and source id.
    /// </summary>
    /// <param name="certificationId">The certification.</param>
    /// <param name="source">The source.</param>
    /// <param name="sourceId">The source id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The certificate, or <see langword="null"/>.</returns>
    Task<CertificationRecord> FindIssuedAsync(string certificationId, string source, string sourceId, CancellationToken cancellationToken = default);
}
