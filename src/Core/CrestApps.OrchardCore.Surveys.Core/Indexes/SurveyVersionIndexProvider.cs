using CrestApps.OrchardCore.Surveys.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Maps a <see cref="SurveyVersion"/> to its <see cref="SurveyVersionIndex"/> row.
/// </summary>
public sealed class SurveyVersionIndexProvider : IndexProvider<SurveyVersion>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyVersionIndexProvider"/> class.
    /// </summary>
    public SurveyVersionIndexProvider()
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<SurveyVersion> context)
        => context.For<SurveyVersionIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(item => new SurveyVersionIndex
            {
                ItemId = item.ItemId,
                SurveyId = item.SurveyId,
                Number = item.Number,
                PublishedUtc = item.PublishedUtc,
            });
}
