using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Certifications.Core.Requirements;

/// <summary>
/// A quiz passed since the cycle started, with at least the minimum percent when one is set. Only attempts the site
/// scored count (the quiz's survey responses, completed and passed through an enrollment that counts): a voided attempt
/// never does, and nothing a person reports about themselves does.
/// </summary>
public sealed class AssessmentPassedRequirement : ICertificationRequirement
{
    private readonly ISurveyResponseQueries _responses;
    private readonly EnrollmentStore _enrollments;
    private readonly IContentManager _contentManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssessmentPassedRequirement"/> class.
    /// </summary>
    /// <param name="responses">The survey response queries.</param>
    /// <param name="enrollments">The enrollment store.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public AssessmentPassedRequirement(
        ISurveyResponseQueries responses,
        EnrollmentStore enrollments,
        IContentManager contentManager,
        IStringLocalizer<AssessmentPassedRequirement> stringLocalizer)
    {
        _responses = responses;
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
        var surveyId = await SurveyIdAsync(context.Requirement.TargetId);

        if (string.IsNullOrEmpty(surveyId) || string.IsNullOrEmpty(context.UserId))
        {
            return null;
        }

        var (_, responses) = await _responses.ListAsync(new SurveyResponseQuery
        {
            SurveyIds = [surveyId],
            UserId = context.UserId,
            Statuses = [SurveyResponseStatus.Completed],
            Take = SurveysConstants.PageSize,
        }, cancellationToken);

        // An attempt counts only when it was made in an enrollment that counts: a preview attempt is made in none, and
        // an exam whose enrollment failed or is under review supports nothing while staff decide.
        foreach (var response in responses
            .Where(response => response.Passed == true && string.Equals(response.UserId, context.UserId, StringComparison.Ordinal))
            .Where(response => context.SinceUtc is null || CompletedUtc(response) >= context.SinceUtc)
            .Where(response => context.Requirement.MinimumGrade is null || response.Percent >= context.Requirement.MinimumGrade)
            .OrderByDescending(CompletedUtc))
        {
            var enrollmentId = LearningSurveyContexts.EnrollmentIdOf(response);

            if (string.IsNullOrEmpty(enrollmentId) || !CompletionEvidence.CountsAsEvidence(await _enrollments.FindByIdAsync(enrollmentId, cancellationToken)))
            {
                continue;
            }

            return new CertificationEvidence
            {
                Kind = Kind,
                TargetId = context.Requirement.TargetId,
                SourceId = response.ItemId,
                Title = await GetTitleAsync(context.Requirement, cancellationToken),
                CompletedUtc = CompletedUtc(response),
                Grade = response.Percent,
            };
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<string>> ListUsersSinceAsync(CertificationRequirement requirement, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var surveyId = await SurveyIdAsync(requirement?.TargetId);
        var users = new HashSet<string>(StringComparer.Ordinal);

        if (string.IsNullOrEmpty(surveyId))
        {
            return users;
        }

        for (var skip = 0; ; skip += SurveysConstants.PageSize)
        {
            var (_, responses) = await _responses.ListAsync(new SurveyResponseQuery
            {
                SurveyIds = [surveyId],
                Statuses = [SurveyResponseStatus.Completed],
                Skip = skip,
                Take = SurveysConstants.PageSize,
            }, cancellationToken);

            foreach (var response in responses.Where(response => response.Passed == true && !string.IsNullOrEmpty(response.UserId) && CompletedUtc(response) >= sinceUtc))
            {
                users.Add(response.UserId);
            }

            if (responses.Count < SurveysConstants.PageSize)
            {
                return users;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<string> GetTitleAsync(CertificationRequirement requirement, CancellationToken cancellationToken = default)
    {
        return string.IsNullOrEmpty(requirement?.TargetId)
            ? null
            : (await _contentManager.GetAsync(requirement.TargetId, VersionOptions.Latest))?.DisplayText;
    }

    private async Task<string> SurveyIdAsync(string quizId)
    {
        if (string.IsNullOrEmpty(quizId))
        {
            return null;
        }

        var quiz = await _contentManager.GetAsync(quizId, VersionOptions.Latest);

        return quiz is not null && quiz.TryGet<QuizPart>(out var part) ? part.SurveyId : null;
    }

    private static DateTime CompletedUtc(SurveyResponse response)
        => response.GradedUtc ?? response.CompletedUtc ?? response.StartedUtc;
}
