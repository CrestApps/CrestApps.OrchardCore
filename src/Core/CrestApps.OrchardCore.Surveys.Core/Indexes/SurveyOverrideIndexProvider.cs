using CrestApps.OrchardCore.Surveys.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Maps a <see cref="SurveyOverride"/> to its <see cref="SurveyOverrideIndex"/> row.
/// </summary>
public sealed class SurveyOverrideIndexProvider : IndexProvider<SurveyOverride>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyOverrideIndexProvider"/> class.
    /// </summary>
    public SurveyOverrideIndexProvider()
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<SurveyOverride> context)
        => context.For<SurveyOverrideIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(item => new SurveyOverrideIndex
            {
                ItemId = item.ItemId,
                SurveyId = item.SurveyId,
                UserId = item.UserId,
                AudienceId = item.AudienceId,
                ContextRef = Cut(item.Context?.ToString(), 255),
                CreatedUtc = item.CreatedUtc,
            });

    private static string Cut(string value, int length)
        => value is not null && value.Length > length ? value.Substring(0, length) : value;
}
