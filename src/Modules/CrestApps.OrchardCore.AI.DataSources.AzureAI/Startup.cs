using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure;
using CrestApps.Core.Infrastructure.Indexing.DataSources;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Recipes;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.DataSources.AzureAI.Drivers;
using CrestApps.OrchardCore.AI.DataSources.AzureAI.Handlers;
using CrestApps.OrchardCore.AI.DataSources.AzureAI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.AzureAI;
using OrchardCore.AzureAI.Core;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.DataSources.AzureAI;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddOrchardCoreIndexingAdapters(AzureAISearchConstants.ProviderName);
        services.TryAddKeyedScoped<IDataSourceContentManager, OrchardCoreAzureAISearchDataSourceContentManager>(AzureAISearchConstants.ProviderName);
        services.TryAddKeyedScoped<IDataSourceDocumentReader, OrchardCoreAzureAISearchDataSourceDocumentReader>(AzureAISearchConstants.ProviderName);
        services.AddIndexProfileHandler<DataSourceAzureAISearchIndexProfileHandler>();
        services.AddScoped<IDocumentIndexHandler, DataSourceAzureAISearchDocumentIndexHandler>();
        services.TryAddScoped<AIDataSourceConnectionPicker>();

        // The tenant's configuration, which only the host's operator controls, approves the indexes a tenant other than
        // the default one may read as the host (review 10, B1).
        services.AddOptions<AzureAISearchHostIdentityOptions>()
            .Configure<IShellConfiguration>((options, configuration) => configuration.GetSection(AzureAISearchHostIdentityOptions.SectionName).Bind(options));
        services.TryAddSingleton<AzureAISearchHostIdentity>();
        services.AddDisplayDriver<AIDataSource, AzureAISearchAIDataSourceDisplayDriver>();
        services.AddAIDataSourceSourceHandler<AzureAISearchAIDataSourceSourceHandler>(AIDataSourceSourceTypes.AzureAISearch);
        services.Configure<IngestionSourceSecretOptions>(options => options.Protect<AIDataSource>(nameof(AzureAISearchSourceMetadata), AIDataSourceProtectionConstants.SourceSecretPurpose));

        // This source reads rows it did not shape, and its handler extracts each document by the configured
        // TitleFieldName and ContentFieldName, so it has to ask which field is which.
        services.Configure<AIDataSourceFieldMappingOptions>(options => options.Require(AIDataSourceSourceTypes.AzureAISearch));
        services.Configure<AIDataSourceSourceOptions>(options => options.AddOrUpdate(
            AIDataSourceSourceTypes.AzureAISearch,
            S["Azure AI Search"],
            S["Read source documents from an external Azure AI Search index using a data connection or explicit connection settings."]));

        services.AddAzureAISearchIndexingSource(DataSourceConstants.IndexingTaskType, o =>
        {
            o.DisplayName = S["AI Knowledge Base Index (Azure AI Search)"];
            o.Description = S["Create an Azure AI Search index to store AI knowledge base document embeddings for vector search."];
        });

        services.Configure<AIDataSourceOptions>(options =>
        {
            options.AddFieldMapping(AzureAISearchConstants.ProviderName, IndexingConstants.ContentsIndexSource, mapping =>
            {
                mapping.DefaultKeyField = "ContentItemId";
                mapping.DefaultTitleField = "Content__ContentItem__DisplayText__keyword";
                mapping.DefaultContentField = "Content__ContentItem__FullText";
            });

            options.AddFieldMapping(AzureAISearchConstants.ProviderName, AIConstants.AIDocumentsIndexingTaskType, mapping =>
            {
                mapping.DefaultKeyField = DocumentIndexConstants.ColumnNames.ChunkId;
                mapping.DefaultTitleField = DocumentIndexConstants.ColumnNames.FileName;
                mapping.DefaultContentField = DocumentIndexConstants.ColumnNames.Content;
            });
        });
    }
}
