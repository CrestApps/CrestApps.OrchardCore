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

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<string>> ListUsersSinceAsync(CertificationRequirement requirement, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var enrollments = await _enrollments.ListCompletedForCourseAsync(requirement.TargetId, sinceUtc, 1000, cancellationToken);

        return enrollments.Select(enrollment => enrollment.UserId).Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <inheritdoc/>
    public async Task<string> GetTitleAsync(CertificationRequirement requirement, CancellationToken cancellationToken = default)
    {
        return string.IsNullOrEmpty(requirement.TargetId)
            ? null
            : (await _contentManager.GetAsync(requirement.TargetId, VersionOptions.Latest))?.DisplayText;
    }
}
