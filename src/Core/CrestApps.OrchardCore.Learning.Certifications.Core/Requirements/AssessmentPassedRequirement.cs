using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Certifications.Core.Requirements;

/// <summary>
/// A quiz passed since the cycle started, with at least the minimum percent when one is set. Only attempts the site
/// scored count: a voided attempt never does, and nothing a person reports about themselves does.
/// </summary>
public sealed class AssessmentPassedRequirement : ICertificationRequirement
{
    private readonly AssessmentAttemptStore _attempts;
    private readonly EnrollmentStore _enrollments;
    private readonly IContentManager _contentManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssessmentPassedRequirement"/> class.
    /// </summary>
    public AssessmentPassedRequirement(AssessmentAttemptStore attempts, EnrollmentStore enrollments, IContentManager contentManager, IStringLocalizer<AssessmentPassedRequirement> stringLocalizer)
    {
        _attempts = attempts;
        _enrollments = enrollments;
        _contentManager = contentManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Kind => CertificationsConstants.RequirementKinds.AssessmentPassed;

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["Assessment passed"];

    /// <inheritdoc/>
    public async Task<CertificationEvidence> FindEvidenceAsync(CertificationRequirementContext context, CancellationToken cancellationToken = default)
    {
        var attempts = await _attempts.ListAsync(context.UserId, context.Requirement.TargetId, cancellationToken);
        AssessmentAttempt best = null;

        // An attempt counts only when it was made in an enrollment that counts: a preview attempt is made in none, and
        // an exam whose enrollment failed or is under review supports nothing while staff decide.
        foreach (var attempt in attempts
            .Where(IsScoredPass)
            .Where(attempt => context.SinceUtc is null || CompletedUtc(attempt) >= context.SinceUtc)
            .Where(attempt => context.Requirement.MinimumGrade is null || attempt.Percent >= context.Requirement.MinimumGrade)
            .Where(attempt => !string.IsNullOrEmpty(attempt.EnrollmentId))
            .OrderByDescending(CompletedUtc))
        {
            if (CourseCompletedRequirement.CountsAsEvidence(await _enrollments.FindByIdAsync(attempt.EnrollmentId, cancellationToken)))
            {
                best = attempt;

                break;
            }
        }

        if (best is null)
        {
            return null;
        }

        return new CertificationEvidence
        {
            Kind = Kind,
            TargetId = context.Requirement.TargetId,
            SourceId = best.ItemId,
            Title = best.QuizTitle,
            CompletedUtc = CompletedUtc(best),
            Grade = best.Percent,
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<string>> ListUsersSinceAsync(CertificationRequirement requirement, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        return await _attempts.ListPassedUserIdsSinceAsync(requirement.TargetId, sinceUtc, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<string> GetTitleAsync(CertificationRequirement requirement, CancellationToken cancellationToken = default)
    {
        return string.IsNullOrEmpty(requirement.TargetId)
            ? null
            : (await _contentManager.GetAsync(requirement.TargetId, VersionOptions.Latest))?.DisplayText;
    }

    private static bool IsScoredPass(AssessmentAttempt attempt)
        => attempt.Passed == true && attempt.Status is AttemptStatus.Submitted or AttemptStatus.Graded;

    private static DateTime CompletedUtc(AssessmentAttempt attempt)
        => attempt.GradedUtc ?? attempt.SubmittedUtc ?? attempt.StartedUtc;
}
