using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Services;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.DataSources;
using CrestApps.Core.Infrastructure.Indexing.Models;
using CrestApps.Core.Services;
using CrestApps.Core.Templates.Services;
using Cysharp.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.AI.Handlers;

internal sealed class DataSourcePreemptiveRagHandler : IPreemptiveRagHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly ITemplateService _templateService;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAITextNormalizer _textNormalizer;
    private readonly AIDataSourceOptions _options;
    private readonly ILogger _logger;

    public DataSourcePreemptiveRagHandler(
        IServiceProvider serviceProvider,
        IAIClientFactory aiClientFactory,
        ITemplateService templateService,
        IAIDeploymentManager deploymentManager,
        IAITextNormalizer textNormalizer,
        IOptions<AIDataSourceOptions> options,
        ILogger<DataSourcePreemptiveRagHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _aiClientFactory = aiClientFactory;
        _templateService = templateService;
        _deploymentManager = deploymentManager;
        _textNormalizer = textNormalizer;
        _options = options.Value;
        _logger = logger;
    }

    public ValueTask<bool> CanHandleAsync(OrchestrationContextBuiltContext context)
    {
        if (context.OrchestrationContext.CompletionContext == null ||
            string.IsNullOrEmpty(context.OrchestrationContext.CompletionContext.DataSourceId))
        {
            return ValueTask.FromResult(false);
        }

        return ValueTask.FromResult(
            _serviceProvider.GetService<ICatalog<AIDataSource>>() != null &&
            _serviceProvider.GetService<ISearchIndexProfileStore>() != null);
    }

    public async Task HandleAsync(PreemptiveRagContext context)
    {
        var ragMetadata = GetRagMetadata(context.Resource);

        try
        {
            await InjectPreemptiveRagContextAsync(context, ragMetadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during preemptive RAG injection for data source '{DataSourceId}'.",
                context.OrchestrationContext.CompletionContext.DataSourceId);
        }
    }

    private async Task InjectPreemptiveRagContextAsync(PreemptiveRagContext context, AIDataSourceRagMetadata ragMetadata)
    {
        var dataSourceCatalog = _serviceProvider.GetService<ICatalog<AIDataSource>>();
        var indexProfileStore = _serviceProvider.GetService<ISearchIndexProfileStore>();

        if (dataSourceCatalog == null || indexProfileStore == null)
        {
            return;
        }

        var orchestrationContext = context.OrchestrationContext;
        var dataSourceId = orchestrationContext.CompletionContext.DataSourceId;
        var dataSource = await dataSourceCatalog.FindByIdAsync(dataSourceId);

        if (dataSource == null || string.IsNullOrEmpty(dataSource.AIKnowledgeBaseIndexProfileName))
        {
            return;
        }

        var indexProfile = await indexProfileStore.FindByNameAsync(dataSource.AIKnowledgeBaseIndexProfileName);

        if (indexProfile == null)
        {
            return;
        }

        var contentManager = _serviceProvider.GetKeyedService<IDataSourceContentManager>(indexProfile.ProviderName);

        if (contentManager == null)
        {
            return;
        }

        var profileMetadata = SearchIndexProfileEmbeddingMetadataAccessor.GetMetadata(indexProfile);
        var embeddingGenerator = await EmbeddingDeploymentResolver.CreateEmbeddingGeneratorAsync(
            _deploymentManager,
            _aiClientFactory,
            profileMetadata,
            indexProfile.EmbeddingDeploymentId);

        if (embeddingGenerator == null)
        {
            return;
        }

        await SearchAndInjectContextAsync(context, ragMetadata, indexProfile, contentManager, embeddingGenerator);
    }

    private async Task SearchAndInjectContextAsync(
        PreemptiveRagContext context,
        AIDataSourceRagMetadata ragMetadata,
        SearchIndexProfile indexProfile,
        IDataSourceContentManager contentManager,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        var orchestrationContext = context.OrchestrationContext;
        var dataSourceId = orchestrationContext.CompletionContext.DataSourceId;
        var embeddings = await embeddingGenerator.GenerateAsync(context.Queries);

        if (embeddings == null || embeddings.Count == 0)
        {
            return;
        }

        var topN = _options.GetTopNDocuments(ragMetadata?.TopNDocuments);

        string providerFilter = null;

        if (!string.IsNullOrWhiteSpace(ragMetadata?.Filter))
        {
            var filterTranslator = _serviceProvider.GetKeyedService<IODataFilterTranslator>(indexProfile.ProviderName);

            if (filterTranslator != null)
            {
                providerFilter = filterTranslator.Translate(ragMetadata.Filter);
            }
        }

        var allResults = new List<DataSourceSearchResult>();
        var seenChunkIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var embedding in embeddings)
        {
            if (embedding?.Vector == null)
            {
                continue;
            }

            var results = await contentManager.SearchAsync(
                indexProfile,
                embedding.Vector.ToArray(),
                dataSourceId,
                topN,
                providerFilter);

            if (results == null)
            {
                continue;
            }

            foreach (var result in results)
            {
                var chunkKey = $"{result.ReferenceId}:{result.ChunkIndex}";

                if (seenChunkIds.Add(chunkKey))
                {
                    allResults.Add(result);
                }
            }
        }

        var strictness = _options.GetStrictness(ragMetadata?.Strictness);
        var query = allResults.AsEnumerable();

        if (strictness > 0)
        {
            var threshold = strictness / (float)AIDataSourceOptions.MaxStrictness;
            query = query.Where(result => result.Score >= threshold);
        }

        var finalResults = query
            .OrderByDescending(result => result.Score)
            .Take(topN)
            .ToList();

        if (finalResults.Count == 0)
        {
            return;
        }

        using var stringBuilder = ZString.CreateStringBuilder();

        var templateArguments = new Dictionary<string, object>();

        if (!orchestrationContext.DisableTools)
        {
            templateArguments["searchToolName"] = SystemToolNames.SearchDataSources;
        }

        var header = await _templateService.RenderAsync(AITemplateIds.DataSourceContextHeader, templateArguments);

        if (!string.IsNullOrEmpty(header))
        {
            stringBuilder.AppendLine();
            stringBuilder.AppendLine();
            stringBuilder.Append(header);
        }

        var invocationContext = AIInvocationScope.Current;
        var seenReferences = new Dictionary<string, (int Index, string Title, string ReferenceType)>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in finalResults)
        {
            if (string.IsNullOrWhiteSpace(result.Content))
            {
                continue;
            }

            var hasReference = !string.IsNullOrEmpty(result.ReferenceId);

            if (hasReference && !seenReferences.ContainsKey(result.ReferenceId))
            {
                seenReferences[result.ReferenceId] = (
                    invocationContext?.NextReferenceIndex() ?? seenReferences.Count + 1,
                    _textNormalizer.NormalizeTitle(result.Title),
                    result.ReferenceType);
            }

            var referenceIndex = hasReference && seenReferences.TryGetValue(result.ReferenceId, out var entry)
                ? entry.Index
                : invocationContext?.NextReferenceIndex() ?? seenReferences.Count + 1;

            stringBuilder.AppendLine("---");
            stringBuilder.Append("[doc:");
            stringBuilder.Append(referenceIndex);
            stringBuilder.Append("] ");
            stringBuilder.AppendLine(result.Content);
        }

        if (seenReferences.Count > 0)
        {
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("References:");

            var citationMap = new Dictionary<string, AICompletionReference>();

            foreach (var (referenceId, value) in seenReferences)
            {
                stringBuilder.Append("[doc:");
                stringBuilder.Append(value.Index);
                stringBuilder.Append("] = {ReferenceId: \"");
                stringBuilder.Append(referenceId);
                stringBuilder.Append('"');

                if (!string.IsNullOrWhiteSpace(value.Title))
                {
                    stringBuilder.Append(", Title: \"");
                    stringBuilder.Append(value.Title);
                    stringBuilder.Append('"');
                }

                stringBuilder.AppendLine("}");

                var template = $"[doc:{value.Index}]";
                citationMap[template] = new AICompletionReference
                {
                    Text = string.IsNullOrWhiteSpace(value.Title) ? template : value.Title,
                    Title = value.Title,
                    Index = value.Index,
                    ReferenceId = referenceId,
                    ReferenceType = value.ReferenceType,
                };
            }

            orchestrationContext.Properties["DataSourceReferences"] = citationMap;
        }

        orchestrationContext.SystemMessageBuilder.Append(stringBuilder);
    }

    private static AIDataSourceRagMetadata GetRagMetadata(object resource)
    {
        if (resource is AIProfile profile &&
            profile.TryGet<AIDataSourceRagMetadata>(out var ragMetadata))
        {
            return ragMetadata;
        }

        if (resource is ChatInteraction interaction &&
            interaction.TryGet<AIDataSourceRagMetadata>(out var interactionRagMetadata))
        {
            return interactionRagMetadata;
        }

        return null;
    }
}
