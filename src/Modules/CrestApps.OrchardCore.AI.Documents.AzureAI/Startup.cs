using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Documents.AzureAI.Handlers;
using CrestApps.OrchardCore.AI.Documents.AzureAI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.AzureAI;
using OrchardCore.AzureAI.Core;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Documents.AzureAI;

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
        services.AddIndexProfileHandler<AIDocumentAzureAISearchIndexProfileHandler>();
        services.AddScoped<IDocumentIndexHandler, AIDocumentAzureAISearchDocumentIndexHandler>();
        services.AddKeyedScoped<IVectorSearchService, AzureAISearchVectorSearchService>(AzureAISearchConstants.ProviderName);
        services.AddAzureAISearchIndexingSource(AIConstants.AIDocumentsIndexingTaskType, o =>
        {
            o.DisplayName = S["AI Documents (Azure AI Search)"];
            o.Description = S["Create an Azure AI Search index for AI documents."];
        });
    }
}
