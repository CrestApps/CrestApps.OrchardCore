using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.Options;
using YesSql;
using YesSql.Indexes;
using YesSql.Services;
using YesSql.Sql;

namespace CrestApps.OrchardCore.AI.Core.Integrations;

/// <summary>
/// Stores integration connections.
/// </summary>
public interface IAIIntegrationConnectionStore : ICatalog<AIIntegrationConnection>
{
    /// <summary>
    /// Lists the connections to some providers.
    /// </summary>
    /// <param name="providers">The providers.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The connections, by name.</returns>
    Task<IReadOnlyList<AIIntegrationConnection>> ListByProvidersAsync(IEnumerable<string> providers, CancellationToken cancellationToken = default);
}

/// <summary>
/// Finds integration connections by name and provider.
/// </summary>
public sealed class AIIntegrationConnectionIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the provider.
    /// </summary>
    public string Provider { get; set; }
}

/// <summary>
/// Maps integration connections to their index.
/// </summary>
public sealed class AIIntegrationConnectionIndexProvider : IndexProvider<AIIntegrationConnection>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIIntegrationConnectionIndexProvider"/> class.
    /// </summary>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    public AIIntegrationConnectionIndexProvider(IOptions<YesSqlStoreOptions> options)
    {
        CollectionName = options.Value.AICollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AIIntegrationConnection> context)
    {
        context.For<AIIntegrationConnectionIndex>()
            .Map(connection => new AIIntegrationConnectionIndex
            {
                ItemId = connection.ItemId,
                Name = connection.Name,
                Provider = connection.Provider,
            });
    }
}

/// <summary>
/// Creates the integration connection index table.
/// </summary>
public static class AIIntegrationConnectionIndexSchema
{
    /// <summary>
    /// Creates the table.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    /// <returns>A task that completes when the table exists.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder, YesSqlStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(options);

        await schemaBuilder.CreateMapIndexTableAsync<AIIntegrationConnectionIndex>(table => table
            .Column<string>(nameof(AIIntegrationConnectionIndex.ItemId), column => column.WithLength(26))
            .Column<string>(nameof(AIIntegrationConnectionIndex.Name), column => column.WithLength(255))
            .Column<string>(nameof(AIIntegrationConnectionIndex.Provider), column => column.WithLength(50)),
            collection: options.AICollectionName);
    }
}

/// <summary>
/// Stores integration connections in the AI collection.
/// </summary>
public sealed class AIIntegrationConnectionStore : DocumentCatalog<AIIntegrationConnection, AIIntegrationConnectionIndex>, IAIIntegrationConnectionStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIIntegrationConnectionStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="options">The YesSql store options, which name the AI collection.</param>
    public AIIntegrationConnectionStore(ISession session, IOptions<YesSqlStoreOptions> options)
        : base(session)
    {
        CollectionName = options.Value.AICollectionName;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AIIntegrationConnection>> ListByProvidersAsync(IEnumerable<string> providers, CancellationToken cancellationToken = default)
    {
        var wanted = (providers ?? []).ToArray();
        var connections = await Session.Query<AIIntegrationConnection, AIIntegrationConnectionIndex>(index => index.Provider.IsIn(wanted), collection: CollectionName)
            .OrderBy(index => index.Name)
            .ListAsync(cancellationToken);

        return connections.ToList();
    }
}
