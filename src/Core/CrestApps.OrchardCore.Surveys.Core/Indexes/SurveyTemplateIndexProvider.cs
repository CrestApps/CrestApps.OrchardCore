using CrestApps.OrchardCore.Surveys.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Maps a <see cref="SurveyTemplate"/> to its <see cref="SurveyTemplateIndex"/> row.
/// </summary>
public sealed class SurveyTemplateIndexProvider : IndexProvider<SurveyTemplate>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyTemplateIndexProvider"/> class.
    /// </summary>
    public SurveyTemplateIndexProvider()
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<SurveyTemplate> context)
        => context.For<SurveyTemplateIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(item => new SurveyTemplateIndex
            {
                ItemId = item.ItemId,
                Name = item.Name,
                Title = item.Title is not null && item.Title.Length > 255 ? item.Title.Substring(0, 255) : item.Title,
                Kind = item.Kind,
                Position = item.Position,
            });
}
