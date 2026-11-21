using CrestApps.OrchardCore.Surveys.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Maps a <see cref="SurveyParticipation"/> to its <see cref="SurveyParticipationIndex"/> row.
/// </summary>
public sealed class SurveyParticipationIndexProvider : IndexProvider<SurveyParticipation>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyParticipationIndexProvider"/> class.
    /// </summary>
    public SurveyParticipationIndexProvider()
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<SurveyParticipation> context)
        => context.For<SurveyParticipationIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(item => new SurveyParticipationIndex
            {
                ItemId = item.ItemId,
                SurveyId = item.SurveyId,
                RespondentKey = item.RespondentKey,
                ContextKey = Cut(item.ContextKey, 255),
                Day = item.Day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                StartedCount = item.StartedCount,
                CompletedCount = item.CompletedCount,
            });

    private static string Cut(string value, int length)
        => value is not null && value.Length > length ? value.Substring(0, length) : value;
}
