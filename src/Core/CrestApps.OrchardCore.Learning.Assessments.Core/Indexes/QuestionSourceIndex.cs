using CrestApps.OrchardCore.Learning.Assessments.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Lists.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;

/// <summary>
/// The latest version of each question imported with a source identifier, such as a QTI item identifier, by its
/// question bank, so importing the same file again finds the question it made.
/// </summary>
public sealed class QuestionSourceIndex : MapIndex
{
    /// <summary>Gets or sets the question.</summary>
    public string ContentItemId { get; set; }

    /// <summary>Gets or sets the source identifier.</summary>
    public string SourceIdentifier { get; set; }

    /// <summary>Gets or sets the question bank, or <see langword="null"/>.</summary>
    public string QuestionBankId { get; set; }
}

/// <summary>
/// Indexes the latest version of the questions that have a source identifier.
/// </summary>
public sealed class QuestionSourceIndexProvider : IndexProvider<ContentItem>
{
    /// <summary>
    /// The longest source identifier indexed.
    /// </summary>
    public const int MaxIdentifierLength = 255;

    /// <inheritdoc />
    public override void Describe(DescribeContext<ContentItem> context)
        => context.For<QuestionSourceIndex>()
            .When(item => item.Latest && !string.IsNullOrEmpty(item.Get<QuestionPart>(nameof(QuestionPart))?.SourceIdentifier))
            .Map(item => new QuestionSourceIndex
            {
                ContentItemId = item.ContentItemId,
                SourceIdentifier = Truncate(item.Get<QuestionPart>(nameof(QuestionPart)).SourceIdentifier),
                QuestionBankId = item.Get<ContainedPart>(nameof(ContainedPart))?.ListContentItemId,
            });

    /// <summary>
    /// Creates the table.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateTableAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<QuestionSourceIndex>(table => table
            .Column<string>(nameof(QuestionSourceIndex.ContentItemId), column => column.WithLength(26))
            .Column<string>(nameof(QuestionSourceIndex.SourceIdentifier), column => column.WithLength(MaxIdentifierLength))
            .Column<string>(nameof(QuestionSourceIndex.QuestionBankId), column => column.Nullable().WithLength(26)));

        await schemaBuilder.AlterIndexTableAsync<QuestionSourceIndex>(table =>
            table.CreateIndex("IDX_QuestionSourceIndex_Source", nameof(QuestionSourceIndex.SourceIdentifier), nameof(QuestionSourceIndex.QuestionBankId)));
    }

    /// <summary>
    /// Keeps an identifier within the indexed length.
    /// </summary>
    /// <param name="identifier">The identifier.</param>
    public static string Truncate(string identifier)
        => identifier is { Length: > MaxIdentifierLength } ? identifier[..MaxIdentifierLength] : identifier;
}
