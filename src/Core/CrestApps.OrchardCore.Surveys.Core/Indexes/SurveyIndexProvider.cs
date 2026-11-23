using CrestApps.OrchardCore.Surveys.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Maps a <see cref="Survey"/> to its <see cref="SurveyIndex"/> row.
/// </summary>
public sealed class SurveyIndexProvider : IndexProvider<Survey>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyIndexProvider"/> class.
    /// </summary>
    public SurveyIndexProvider()
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<Survey> context)
        => context.For<SurveyIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(item => new SurveyIndex
            {
                ItemId = item.ItemId,
                Name = item.Name,
                Title = Cut(item.Title, 255),
                Kind = item.Kind,
                Status = item.Status.ToString(),
                OwnerRef = Cut(item.Owner?.ToString(), 255),
                PublishedVersionNumber = item.PublishedVersionNumber,
                ModifiedUtc = item.ModifiedUtc ?? item.CreatedUtc,
                OwnerId = Cut(item.OwnerId, 26),
                Anonymity = (item.Access?.Anonymity ?? AnonymityMode.Identified).ToString(),
                MinimumGroupSize = item.Access?.MinimumGroupSize,
            });

    private static string Cut(string value, int length)
        => value is not null && value.Length > length ? value.Substring(0, length) : value;
}
