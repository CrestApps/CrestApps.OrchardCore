using CrestApps.OrchardCore.Surveys.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Maps each tag of a <see cref="Survey"/> to a <see cref="SurveyTagIndex"/> row.
/// </summary>
public sealed class SurveyTagIndexProvider : IndexProvider<Survey>
{
    /// <summary>
    /// The longest tag the index holds.
    /// </summary>
    public const int MaxTagLength = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyTagIndexProvider"/> class.
    /// </summary>
    public SurveyTagIndexProvider()
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<Survey> context)
        => context.For<SurveyTagIndex>()
            .ExclusiveToDocumentType()
            .Map(item => (item.Tags ?? [])
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(Normalize)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(tag => new SurveyTagIndex
                {
                    SurveyId = item.ItemId,
                    Tag = tag,
                    OwnerId = item.OwnerId is { Length: > 26 } ? item.OwnerId.Substring(0, 26) : item.OwnerId,
                }));

    /// <summary>
    /// Gets a tag as the index holds it: trimmed and cut to <see cref="MaxTagLength"/> characters.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <returns>The tag as indexed.</returns>
    public static string Normalize(string tag)
    {
        var trimmed = tag?.Trim() ?? string.Empty;

        return trimmed.Length > MaxTagLength ? trimmed.Substring(0, MaxTagLength) : trimmed;
    }
}
