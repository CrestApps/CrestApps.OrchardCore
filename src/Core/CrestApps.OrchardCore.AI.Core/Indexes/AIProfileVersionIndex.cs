using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.Options;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Core.Indexes;

/// <summary>
/// One row per <see cref="AIProfileVersion"/>, by profile and version number.
/// </summary>
public sealed class AIProfileVersionIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the identifier of the version.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the profile.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the version number.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets when the version was saved.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// Maps <see cref="AIProfileVersion"/> documents to <see cref="AIProfileVersionIndex"/> rows in the AI collection.
/// </summary>
public sealed class AIProfileVersionIndexProvider : IndexProvider<AIProfileVersion>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileVersionIndexProvider"/> class.
    /// </summary>
    /// <param name="options">The YesSql store options that name the AI collection.</param>
    public AIProfileVersionIndexProvider(IOptions<YesSqlStoreOptions> options)
    {
        CollectionName = options.Value.AICollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AIProfileVersion> context)
    {
        context.For<AIProfileVersionIndex>()
            .Map(version => new AIProfileVersionIndex
            {
                ItemId = version.ItemId,
                ProfileId = version.ProfileId,
                Version = version.Version,
                CreatedUtc = version.CreatedUtc,
            });
    }
}

/// <summary>
/// Creates the table behind <see cref="AIProfileVersionIndex"/>.
/// </summary>
public static class AIProfileVersionIndexSchema
{
    private const int IdLength = 26;

    /// <summary>
    /// Creates the index table and the index a profile's history is read through.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder of the running migration.</param>
    /// <param name="options">The store options that name the AI collection.</param>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder, YesSqlStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(options);

        await schemaBuilder.CreateMapIndexTableAsync<AIProfileVersionIndex>(table => table
            .Column<string>(nameof(AIProfileVersionIndex.ItemId), column => column.WithLength(IdLength))
            .Column<string>(nameof(AIProfileVersionIndex.ProfileId), column => column.WithLength(IdLength))
            .Column<int>(nameof(AIProfileVersionIndex.Version))
            .Column<DateTime>(nameof(AIProfileVersionIndex.CreatedUtc), column => column.NotNull()),
            collection: options.AICollectionName);

        await schemaBuilder.AlterIndexTableAsync<AIProfileVersionIndex>(
            table => table.CreateIndex(
                "IDX_AIProfileVersion_ProfileId",
                nameof(AIProfileVersionIndex.ProfileId),
                nameof(AIProfileVersionIndex.Version),
                "DocumentId"),
            collection: options.AICollectionName);
    }
}
