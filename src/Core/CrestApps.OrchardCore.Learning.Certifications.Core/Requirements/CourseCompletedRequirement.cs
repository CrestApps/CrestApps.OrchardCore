using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Certifications.Core.Requirements;

/// <summary>
/// A course completed since the cycle started, with at least the minimum grade when one is set.
/// </summary>
public sealed class CourseCompletedRequirement : ICertificationRequirement
{
    private readonly EnrollmentStore _enrollments;
    private readonly IContentManager _contentManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseCompletedRequirement"/> class.
    /// </summary>
    public CourseCompletedRequirement(EnrollmentStore enrollments, IContentManager contentManager, IStringLocalizer<CourseCompletedRequirement> stringLocalizer)
    {
        _enrollments = enrollments;
        _contentManager = contentManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Kind => CertificationsConstants.RequirementKinds.CourseCompleted;

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["Course completed"];

    /// <inheritdoc/>
    public async Task<CertificationEvidence> FindEvidenceAsync(CertificationRequirementContext context, CancellationToken cancellationToken = default)
    {
        var enrollments = await _enrollments.ListByCourseForUserAsync(context.UserId, context.Requirement.TargetId, cancellationToken);
        var best = enrollments
            .Where(enrollment => enrollment.Status == EnrollmentStatus.Completed && enrollment.CompletedUtc is not null)
            .Where(CountsAsEvidence)
            .Where(enrollment => !RestsOnABrowserResult(enrollment))
            .Where(enrollment => context.SinceUtc is null || enrollment.CompletedUtc >= context.SinceUtc)
            .Where(enrollment => context.Requirement.MinimumGrade is null || (enrollment.Grade ?? enrollment.Completion?.Grade) >= context.Requirement.MinimumGrade)
            .OrderByDescending(enrollment => enrollment.CompletedUtc)
            .FirstOrDefault();

        if (best is null)
        {
            return null;
        }

        return new CertificationEvidence
        {
            Kind = Kind,
            TargetId = context.Requirement.TargetId,
            SourceId = best.ItemId,
            Title = best.Completion?.CourseTitle,
            CompletedUtc = best.CompletedUtc.Value,
            Grade = best.Grade ?? best.Completion?.Grade,
        };
    }

    /// <summary>
    /// Tells whether an enrollment may support a certificate: a failed one never does, and one under review does not
    /// while staff are deciding.
    /// </summary>
    /// <param name="enrollment">The enrollment.</param>
    /// <returns><see langword="true"/> when it counts.</returns>
    public static bool CountsAsEvidence(Enrollment enrollment)
        => enrollment is not null && enrollment.Passed != false && !enrollment.NeedsReview;

    /// <summary>
    /// Tells whether a completion rests on a result the browser reported, such as a learning package's own status or
    /// score: the result of a required item of the completion snapshot was reported by the browser and its item did not
    /// let such a result count toward a certification. The browser can be changed by the learner, so by default such a
    /// completion supports no certificate. A share of a page or video viewed is not a result and does not count here.
    /// </summary>
    /// <param name="enrollment">The completed enrollment.</param>
    /// <returns><see langword="true"/> when the completion cannot support a certificate.</returns>
    public static bool RestsOnABrowserResult(Enrollment enrollment)
        => enrollment?.Completion?.Items is { } items
        && items.Any(item => item.Required && item.ResultSelfReported && !item.AllowSelfReported);

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<string>> ListUsersSinceAsync(CertificationRequirement requirement, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        return await _enrollments.ListCompletedUserIdsForCourseAsync(requirement.TargetId, sinceUtc, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<string> GetTitleAsync(CertificationRequirement requirement, CancellationToken cancellationToken = default)
    {
        return string.IsNullOrEmpty(requirement.TargetId)
            ? null
            : (await _contentManager.GetAsync(requirement.TargetId, VersionOptions.Latest))?.DisplayText;
    }
}
