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
                .Where(tag => tag.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(tag => new SurveyTagIndex
                {
                    SurveyId = item.ItemId,
                    Tag = tag,
                    OwnerId = item.OwnerId is { Length: > 26 } ? item.OwnerId.Substring(0, 26) : item.OwnerId,
                }));

    /// <summary>
    /// Gets a tag as the index holds it: control characters (a line break, a tab) turned into spaces, trimmed, and cut
    /// to <see cref="MaxTagLength"/> characters. A tag is one line of text, so it never carries a line break into a list,
    /// a filter or a log line.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <returns>The tag as indexed.</returns>
    public static string Normalize(string tag)
    {
        if (string.IsNullOrEmpty(tag))
        {
            return string.Empty;
        }

        var text = tag.Any(char.IsControl) ? string.Concat(tag.Select(character => char.IsControl(character) ? ' ' : character)) : tag;
        var trimmed = text.Trim();

        return trimmed.Length > MaxTagLength ? trimmed.Substring(0, MaxTagLength) : trimmed;
    }
}
