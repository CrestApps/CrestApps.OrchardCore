using CrestApps.OrchardCore.AI.Core.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.DataSources;

/// <summary>
/// Wraps the knowledge base search of every provider (Elasticsearch, Azure AI Search, PostgreSQL and any other that
/// registers an <see cref="CrestApps.Core.Infrastructure.Indexing.DataSources.IDataSourceContentManager"/>) so that the
/// registered <see cref="IAIDataSourceResultFilter"/> instances trim every search. It runs after the other startups,
/// because the provider features depend on this one and register their content managers later.
/// </summary>
public sealed class ResultFiltersStartup : StartupBase
{
    /// <inheritdoc />
    public override int Order => 10_000;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddOptions<AIDataSourceResultFilterOptions>();
        services.DecorateDataSourceContentManagers();
    }
}
