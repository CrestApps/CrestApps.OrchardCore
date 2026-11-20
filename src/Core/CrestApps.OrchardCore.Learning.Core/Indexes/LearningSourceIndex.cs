using CrestApps.OrchardCore.Learning.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Learning.Core.Indexes;

/// <summary>
/// The courses and course items imported from another system, by where they came from (the source identifier of
/// <see cref="CoursePart"/> or <see cref="CourseItemPart"/>), so importing them again updates them. Only the latest
/// version of a content item is indexed.
/// </summary>
public sealed class LearningSourceIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the content item id.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the content type.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets where the item came from, such as <c>moodle:1a2b3c4d5e6f:course:12:cm:345</c>.
    /// </summary>
    public string SourceIdentifier { get; set; }
}

/// <summary>
/// Indexes the source identifier of courses and course items.
/// </summary>
public sealed class LearningSourceIndexProvider : IndexProvider<ContentItem>
{
    /// <summary>
    /// The longest source identifier kept.
    /// </summary>
    public const int MaxIdentifierLength = 255;

    /// <inheritdoc />
    public override void Describe(DescribeContext<ContentItem> context)
        => context.For<LearningSourceIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .When(item => item.Latest && SourceOf(item) is not null)
            .Map(item => new LearningSourceIndex
            {
                ContentItemId = item.ContentItemId,
                ContentType = item.ContentType,
                SourceIdentifier = SourceOf(item),
            });

    /// <summary>
    /// Creates the table.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateTableAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<LearningSourceIndex>(table => table
            .Column<string>(nameof(LearningSourceIndex.ContentItemId), column => column.WithLength(26))
            .Column<string>(nameof(LearningSourceIndex.ContentType), column => column.WithLength(ContentItemIndex.MaxContentTypeSize))
            .Column<string>(nameof(LearningSourceIndex.SourceIdentifier), column => column.WithLength(MaxIdentifierLength)));

        await schemaBuilder.AlterIndexTableAsync<LearningSourceIndex>(table => table
            .CreateIndex("IDX_LearningSourceIndex_SourceIdentifier", nameof(LearningSourceIndex.SourceIdentifier), "DocumentId"));
    }

    /// <summary>
    /// Gets where a course or a course item came from, cut to the indexed length.
    /// </summary>
    /// <param name="item">The content item.</param>
    /// <returns>The source identifier, or <see langword="null"/>.</returns>
    public static string SourceOf(ContentItem item)
    {
        var source = item?.Get<CoursePart>(nameof(CoursePart))?.SourceIdentifier
            ?? item?.Get<CourseItemPart>(nameof(CourseItemPart))?.SourceIdentifier;

        return string.IsNullOrWhiteSpace(source) ? null : Truncate(source.Trim());
    }

    /// <summary>
    /// Cuts a source identifier to the indexed length.
    /// </summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>The identifier, at most <see cref="MaxIdentifierLength"/> characters.</returns>
    public static string Truncate(string identifier)
        => identifier is { Length: > MaxIdentifierLength } ? identifier[..MaxIdentifierLength] : identifier;
}
