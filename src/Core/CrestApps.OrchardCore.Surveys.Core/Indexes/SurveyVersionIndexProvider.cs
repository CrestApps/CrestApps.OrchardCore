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
                PublishedBy = item.PublishedBy is { Length: > 0 } publishedBy && publishedBy.Length <= 26 ? publishedBy : string.Empty,
                Notes = NotesOf(item.Notes),
            });

    /// <summary>
    /// Gets the notes the index keeps: at most <see cref="SurveyVersionIndex.NotesLength"/> characters, the longer ones
    /// cut with an ellipsis.
    /// </summary>
    /// <param name="notes">The notes.</param>
    /// <returns>The notes to index, or null.</returns>
    public static string NotesOf(string notes)
        => string.IsNullOrWhiteSpace(notes)
            ? null
            : notes.Length <= SurveyVersionIndex.NotesLength ? notes : notes[..(SurveyVersionIndex.NotesLength - 1)] + "\u2026";
}
