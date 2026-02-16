using CrestApps.OrchardCore.AI.DataSources.MongoDB.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.DataSources.MongoDB;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddKeyedScoped<IDataSourceVectorSearchService, DataSourceMongoDBVectorSearchService>(
            MongoDBDataSourceConstants.ProviderName);
        services.AddKeyedScoped<IDataSourceDocumentReader, DataSourceMongoDBDocumentReader>(
            MongoDBDataSourceConstants.ProviderName);
        services.AddKeyedSingleton<IODataFilterTranslator, MongoDBODataFilterTranslator>(
            MongoDBDataSourceConstants.ProviderName);
    }
}
