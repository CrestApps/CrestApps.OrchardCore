using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Certifications.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Certifications.Services;

/// <summary>
/// Offers to revoke, from the review card of an enrollment, the certificates that rest on it.
/// </summary>
internal sealed class CertificationReviewActionProvider : IEnrollmentReviewActionProvider
{
    private readonly DefaultCertificationPathService _paths;
    private readonly ICertificationRecordService _records;
    private readonly ICertificationManager _certifications;
    private readonly IServiceProvider _services;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    internal readonly IStringLocalizer S;

    public CertificationReviewActionProvider(
        DefaultCertificationPathService paths,
        ICertificationRecordService records,
        ICertificationManager certifications,
        IServiceProvider services,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator,
        IStringLocalizer<CertificationReviewActionProvider> stringLocalizer)
    {
        _paths = paths;
        _records = records;
        _certifications = certifications;
        _services = services;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        S = stringLocalizer;
    }

    public async Task<IEnumerable<EnrollmentReviewAction>> GetActionsAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
    {
        var attempts = _services.GetService<AssessmentAttemptStore>();
        var affected = await _paths.ListRestingOnAsync(
            enrollment,
            attempts is null ? null : async attemptId => (await attempts.FindByIdAsync(attemptId, cancellationToken))?.EnrollmentId,
            cancellationToken);
        var actions = new List<EnrollmentReviewAction>();
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null)
        {
            return actions;
        }

        foreach (var (_, recordId) in affected.DistinctBy(item => item.RecordId))
        {
            var record = await _records.FindByIdAsync(recordId, cancellationToken);

            if (record is null || record.Status == CertificationStatus.Revoked)
            {
                continue;
            }

            var certification = await _certifications.FindByIdAsync(record.CertificationId, cancellationToken);
            var name = string.Join(' ', new[] { certification?.Name, record.Number }.Where(value => !string.IsNullOrEmpty(value)));

            actions.Add(new EnrollmentReviewAction
            {
                Text = S["Revoke the certificate {0}", name],
                Hint = S["The certificate rests on this enrollment. Revoking it shows it as revoked on its verification page at once; the review stays open until you clear it."],
                Confirmation = S["Revoke the certificate {0}?", name],
                Url = _linkGenerator.GetPathByAction(httpContext, "RevokeFromReview", "CertificationPaths", new { area = "CrestApps.OrchardCore.Learning.Certifications", enrollmentId = enrollment.ItemId, recordId }),
            });
        }

        return actions;
    }
}
