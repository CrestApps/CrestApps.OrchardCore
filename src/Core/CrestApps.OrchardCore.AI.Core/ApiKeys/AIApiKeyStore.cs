using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.Options;
using YesSql;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Core.ApiKeys;

/// <summary>
/// Stores API keys.
/// </summary>
public interface IAIApiKeyStore : ICatalog<AIApiKey>
{
    /// <summary>
    /// Finds the key that starts with a prefix.
    /// </summary>
    /// <param name="prefix">The prefix.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The key, or <see langword="null"/>.</returns>
    Task<AIApiKey> FindByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}

/// <summary>
/// Finds API keys by name and prefix.
/// </summary>
public sealed class AIApiKeyIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the prefix.
    /// </summary>
    public string Prefix { get; set; }
}

/// <summary>
/// Maps API keys to their index.
/// </summary>
public sealed class AIApiKeyIndexProvider : IndexProvider<AIApiKey>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIApiKeyIndexProvider"/> class.
    /// </summary>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    public AIApiKeyIndexProvider(IOptions<YesSqlStoreOptions> options)
    {
        CollectionName = options.Value.AICollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AIApiKey> context)
    {
        context.For<AIApiKeyIndex>()
            .Map(key => new AIApiKeyIndex
            {
                ItemId = key.ItemId,
                Name = key.Name,
                Prefix = key.Prefix,
            });
    }
}

/// <summary>
/// Creates the API key index table.
/// </summary>
public static class AIApiKeyIndexSchema
{
    /// <summary>
    /// Creates the table and its index.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    /// <returns>A task that completes when the table exists.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder, YesSqlStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(options);

        await schemaBuilder.CreateMapIndexTableAsync<AIApiKeyIndex>(table => table
            .Column<string>(nameof(AIApiKeyIndex.ItemId), column => column.WithLength(26))
            .Column<string>(nameof(AIApiKeyIndex.Name), column => column.WithLength(255))
            .Column<string>(nameof(AIApiKeyIndex.Prefix), column => column.WithLength(32)),
            collection: options.AICollectionName);

        await schemaBuilder.AlterIndexTableAsync<AIApiKeyIndex>(
            table => table.CreateIndex("IDX_AIApiKey_Prefix", nameof(AIApiKeyIndex.Prefix), "DocumentId"),
            collection: options.AICollectionName);
    }
}

/// <summary>
/// Stores API keys in the AI collection.
/// </summary>
public sealed class AIApiKeyStore : DocumentCatalog<AIApiKey, AIApiKeyIndex>, IAIApiKeyStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIApiKeyStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    public AIApiKeyStore(ISession session, IOptions<YesSqlStoreOptions> options)
        : base(session)
    {
        CollectionName = options.Value.AICollectionName;
    }

    /// <inheritdoc/>
    public async Task<AIApiKey> FindByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return null;
        }

        return await Session.Query<AIApiKey, AIApiKeyIndex>(index => index.Prefix == prefix, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
