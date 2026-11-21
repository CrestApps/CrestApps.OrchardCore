using CrestApps.OrchardCore.Surveys.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Maps a <see cref="SurveyResponse"/> to its <see cref="SurveyResponseIndex"/> row.
/// </summary>
public sealed class SurveyResponseIndexProvider : IndexProvider<SurveyResponse>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponseIndexProvider"/> class.
    /// </summary>
    public SurveyResponseIndexProvider()
    {
        CollectionName = SurveysConstants.ResponsesCollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<SurveyResponse> context)
        => context.For<SurveyResponseIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(item => new SurveyResponseIndex
            {
                ItemId = item.ItemId,
                SurveyId = item.SurveyId,
                SurveyVersionId = item.SurveyVersionId,
                VersionNumber = item.VersionNumber,
                Number = item.Number,
                Status = item.Status.ToString(),
                Grading = item.Grading.ToString(),
                UserId = item.UserId,
                RespondentRef = Cut(item.Respondent?.ToString(), 255),
                ContextRef = Cut(item.Context?.ToString(), 255),
                ContextKind = Cut(item.Context?.Kind, 100),
                CollectorId = item.CollectorId,
                InvitationId = item.InvitationId,
                AttemptNumber = item.AttemptNumber,
                IsTest = item.IsTest,
                StartedUtc = item.StartedUtc,
                LastActivityUtc = item.LastActivityUtc,
                CompletedUtc = item.CompletedUtc,
                DeadlineUtc = item.DeadlineUtc,
                Percent = item.Percent,
                Passed = item.Passed,
                ModifiedUtc = item.ModifiedUtc,
            });

    private static string Cut(string value, int length)
        => value is not null && value.Length > length ? value.Substring(0, length) : value;
}
