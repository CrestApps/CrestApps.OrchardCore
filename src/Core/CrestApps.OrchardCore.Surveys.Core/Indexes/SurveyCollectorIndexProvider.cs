using CrestApps.OrchardCore.Surveys.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Maps a <see cref="SurveyCollector"/> to its <see cref="SurveyCollectorIndex"/> row.
/// </summary>
public sealed class SurveyCollectorIndexProvider : IndexProvider<SurveyCollector>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyCollectorIndexProvider"/> class.
    /// </summary>
    public SurveyCollectorIndexProvider()
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<SurveyCollector> context)
        => context.For<SurveyCollectorIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(item => new SurveyCollectorIndex
            {
                ItemId = item.ItemId,
                SurveyId = item.SurveyId,
                Kind = item.Kind,
                Token = item.Token,
                Status = item.Status.ToString(),
                IsDefault = item.IsDefault,
            });
}
