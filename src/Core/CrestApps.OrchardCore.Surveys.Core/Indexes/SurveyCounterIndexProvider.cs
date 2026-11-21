using CrestApps.OrchardCore.Surveys.Core.Runtime;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Maps a <see cref="SurveyCounter"/> to its <see cref="SurveyCounterIndex"/> row.
/// </summary>
public sealed class SurveyCounterIndexProvider : IndexProvider<SurveyCounter>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyCounterIndexProvider"/> class.
    /// </summary>
    public SurveyCounterIndexProvider()
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<SurveyCounter> context)
        => context.For<SurveyCounterIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(item => new SurveyCounterIndex
            {
                ItemId = item.ItemId,
                SurveyId = item.SurveyId,
                Kind = item.Kind,
            });
}
