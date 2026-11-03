using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.Options;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Core.Indexes;

/// <summary>
/// One row per <see cref="AIContactMemory"/>, by contact.
/// </summary>
public sealed class AIContactMemoryIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the content item id of the contact.
    /// </summary>
    public string ContactId { get; set; }
}

/// <summary>
/// Maps <see cref="AIContactMemory"/> documents to <see cref="AIContactMemoryIndex"/> rows in the AI collection.
/// </summary>
public sealed class AIContactMemoryIndexProvider : IndexProvider<AIContactMemory>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIContactMemoryIndexProvider"/> class.
    /// </summary>
    /// <param name="options">The YesSql store options that name the AI collection.</param>
    public AIContactMemoryIndexProvider(IOptions<YesSqlStoreOptions> options)
    {
        CollectionName = options.Value.AICollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AIContactMemory> context)
    {
        context.For<AIContactMemoryIndex>()
            .Map(memory => new AIContactMemoryIndex { ContactId = memory.ContactId });
    }
}

/// <summary>
/// Creates the table behind <see cref="AIContactMemoryIndex"/>.
/// </summary>
public static class AIContactMemoryIndexSchema
{
    /// <summary>
    /// Creates the index table and the index a contact's note is found by.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder of the running migration.</param>
    /// <param name="options">The store options that name the AI collection.</param>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder, YesSqlStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(options);

        await schemaBuilder.CreateMapIndexTableAsync<AIContactMemoryIndex>(table => table
            .Column<string>(nameof(AIContactMemoryIndex.ContactId), column => column.WithLength(26)),
            collection: options.AICollectionName);

        await schemaBuilder.AlterIndexTableAsync<AIContactMemoryIndex>(
            table => table.CreateIndex(
                "IDX_AIContactMemory_ContactId",
                nameof(AIContactMemoryIndex.ContactId),
                "DocumentId"),
            collection: options.AICollectionName);
    }
}
