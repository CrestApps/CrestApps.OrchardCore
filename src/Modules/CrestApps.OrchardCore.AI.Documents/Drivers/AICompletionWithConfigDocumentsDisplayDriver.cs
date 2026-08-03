using CrestApps.Core;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Documents.Models;
using CrestApps.Core.AI.Documents.Services;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Resilience;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Documents.Services;
using CrestApps.OrchardCore.AI.Documents.ViewModels;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;
using OrchardCore.Workflows.Activities;

namespace CrestApps.OrchardCore.AI.Documents.Drivers;

/// <summary>
/// Display driver that contributes the uploaded knowledgebase documents UI to the Knowledge tab of the
/// <see cref="AICompletionWithConfigTask"/> workflow activity. Uploaded documents are embedded and indexed,
/// keyed by the embedded interaction identifier, so they are retrieved during completion to provide context.
/// </summary>
public sealed class AICompletionWithConfigDocumentsDisplayDriver : DisplayDriver<IActivity, AICompletionWithConfigTask>
{
    private readonly ISiteService _siteService;
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAIDocumentStore _documentStore;
    private readonly IAIDocumentChunkStore _chunkStore;
    private readonly IAIDocumentProcessingService _documentProcessingService;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly IOptions<ChatDocumentsOptions> _extractorOptions;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AICompletionWithConfigDocumentsDisplayDriver"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    /// <param name="indexProfileStore">The index profile store.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="documentStore">The document store.</param>
    /// <param name="chunkStore">The chunk store.</param>
    /// <param name="documentProcessingService">The document processing service.</param>
    /// <param name="deploymentManager">The deployment manager.</param>
    /// <param name="aiClientFactory">The AI client factory.</param>
    /// <param name="extractorOptions">The extractor options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AICompletionWithConfigDocumentsDisplayDriver(
        ISiteService siteService,
        IIndexProfileStore indexProfileStore,
        IServiceProvider serviceProvider,
        IAIDocumentStore documentStore,
        IAIDocumentChunkStore chunkStore,
        IAIDocumentProcessingService documentProcessingService,
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory,
        IOptions<ChatDocumentsOptions> extractorOptions,
        ILogger<AICompletionWithConfigDocumentsDisplayDriver> logger,
        IStringLocalizer<AICompletionWithConfigDocumentsDisplayDriver> stringLocalizer)
    {
        _siteService = siteService;
        _indexProfileStore = indexProfileStore;
        _serviceProvider = serviceProvider;
        _documentStore = documentStore;
        _chunkStore = chunkStore;
        _documentProcessingService = documentProcessingService;
        _deploymentManager = deploymentManager;
        _aiClientFactory = aiClientFactory;
        _extractorOptions = extractorOptions;
        _logger = logger;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AICompletionWithConfigTask activity, BuildEditorContext context)
    {
        return Initialize<AICompletionWithConfigDocumentsViewModel>("AICompletionWithConfigDocuments_Edit", async model =>
        {
            var interaction = activity.Interaction;

            var documentsMetadata = interaction.GetOrCreate<DocumentsMetadata>();

            model.InteractionId = interaction.ItemId;
            model.Documents = documentsMetadata.Documents ?? [];
            model.TopN = documentsMetadata.DocumentTopN ?? 3;
            model.DocumentRetrievalMode = documentsMetadata.RetrievalMode;
            model.DocumentRetrievalModes = DocumentRetrievalModeSelectListBuilder.Build(S, model.DocumentRetrievalMode);

            var settings = await _siteService.GetSettingsAsync<InteractionDocumentSettings>();
            model.IndexProfileName = settings.IndexProfileName;
            model.HasIndexProfile = !string.IsNullOrEmpty(settings.IndexProfileName);

            if (model.HasIndexProfile)
            {
                var indexProfile = await _indexProfileStore.FindByNameAsync(settings.IndexProfileName);

                if (indexProfile != null)
                {
                    var searchService = _serviceProvider.GetKeyedService<IVectorSearchService>(indexProfile.ProviderName);
                    model.HasVectorSearchService = searchService != null;
                }
            }
        }).Location("Content:5#Knowledge;2");
    }

    public override async Task<IDisplayResult> UpdateAsync(AICompletionWithConfigTask activity, UpdateEditorContext context)
    {
        var model = new AICompletionWithConfigDocumentsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var interaction = activity.Interaction;

        if (string.IsNullOrEmpty(interaction.ItemId))
        {
            interaction.ItemId = UniqueId.GenerateId();
        }

        var documentsMetadata = interaction.GetOrCreate<DocumentsMetadata>();
        documentsMetadata.Documents ??= [];
        documentsMetadata.DocumentTopN = model.TopN > 0 ? model.TopN : 3;
        documentsMetadata.RetrievalMode = model.DocumentRetrievalMode;

        if (context.Updater.ModelState.IsValid)
        {
            if (model.RemovedDocumentIds != null && model.RemovedDocumentIds.Length > 0)
            {
                var chunkIdsToRemove = new List<string>();

                foreach (var documentId in model.RemovedDocumentIds)
                {
                    if (string.IsNullOrEmpty(documentId))
                    {
                        continue;
                    }

                    var docInfo = documentsMetadata.Documents.FirstOrDefault(d => d.DocumentId == documentId);

                    if (docInfo == null)
                    {
                        continue;
                    }

                    var document = await _documentStore.FindByIdAsync(documentId);

                    if (document != null)
                    {
                        var chunks = await _chunkStore.GetChunksByAIDocumentIdAsync(document.ItemId);

                        foreach (var chunk in chunks)
                        {
                            chunkIdsToRemove.Add(chunk.ItemId);
                        }

                        await _chunkStore.DeleteByDocumentIdAsync(document.ItemId);
                        await _documentStore.DeleteAsync(document);
                    }

                    documentsMetadata.Documents.Remove(docInfo);
                }

                if (chunkIdsToRemove.Count > 0)
                {
                    ShellScope.AddDeferredTask(scope => RemoveDocumentChunksAsync(scope, chunkIdsToRemove));
                }
            }

            if (model.Files != null && model.Files.Length > 0)
            {
                var chatDeployment = await _deploymentManager.ResolveOrDefaultAsync(
                    AIDeploymentPurpose.Chat,
                    deploymentName: interaction.ChatDeploymentName);
                var embeddingDeployment = await _deploymentManager.ResolveOrDefaultAsync(
                    AIDeploymentPurpose.Embedding,
                    clientName: chatDeployment?.ClientName);
                var embeddingGenerator = embeddingDeployment == null
                    ? null
                    : await _aiClientFactory.CreateEmbeddingGeneratorAsync(embeddingDeployment, builder => builder.UseDefaultResilience());
                var processedDocuments = new List<AIDocument>();

                foreach (var file in model.Files)
                {
                    if (file == null || file.Length == 0)
                    {
                        continue;
                    }

                    var extension = Path.GetExtension(file.FileName);

                    if (!_extractorOptions.Value.EmbeddableFileExtensions.Contains(extension))
                    {
                        context.Updater.ModelState.AddModelError(Prefix, nameof(model.Files),
                            S["File type '{0}' is not supported. Only text-based files are allowed.", extension]);
                        continue;
                    }

                    try
                    {
                        var result = await _documentProcessingService.ProcessFileAsync(
                            file,
                            interaction.ItemId,
                            AIConstants.DocumentReferenceTypes.ChatInteraction,
                            embeddingGenerator);

                        if (!result.Success)
                        {
                            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Files), S["{0}: {1}", file.FileName, result.Error]);
                            continue;
                        }

                        documentsMetadata.Documents.Add(result.DocumentInfo);
                        await _documentStore.CreateAsync(result.Document);

                        foreach (var chunk in result.Chunks)
                        {
                            await _chunkStore.CreateAsync(chunk);
                        }

                        processedDocuments.Add(result.Document);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process file {FileName}", file.FileName.SanitizeLogValue());
                        context.Updater.ModelState.AddModelError(Prefix, nameof(model.Files), S["Failed to process file '{0}'.", file.FileName]);
                    }
                }

                if (processedDocuments.Count > 0)
                {
                    ShellScope.AddDeferredTask(scope => IndexDocumentChunksAsync(scope, processedDocuments));
                }
            }
        }

        interaction.Put(documentsMetadata);

        activity.Interaction = interaction;

        return Edit(activity, context);
    }

    private static async Task IndexDocumentChunksAsync(ShellScope scope, List<AIDocument> documents)
    {
        var services = scope.ServiceProvider;
        var indexStore = services.GetRequiredService<IIndexProfileStore>();
        var indexProfiles = await indexStore.GetByTypeAsync(AIConstants.AIDocumentsIndexingTaskType);

        if (!indexProfiles.Any())
        {
            return;
        }

        var chunkStore = services.GetRequiredService<IAIDocumentChunkStore>();
        var documentIndexHandlers = services.GetRequiredService<IEnumerable<IDocumentIndexHandler>>();
        var logger = services.GetRequiredService<ILogger<AICompletionWithConfigDocumentsDisplayDriver>>();

        foreach (var indexProfile in indexProfiles)
        {
            var documentIndexManager = services.GetKeyedService<IDocumentIndexManager>(indexProfile.ProviderName);

            if (documentIndexManager == null)
            {
                continue;
            }

            var chunkDocuments = new List<DocumentIndex>();

            foreach (var aiDocument in documents)
            {
                var chunks = await chunkStore.GetChunksByAIDocumentIdAsync(aiDocument.ItemId);

                if (chunks.Count == 0)
                {
                    continue;
                }

                foreach (var chunk in chunks)
                {
                    var documentIndex = new DocumentIndex(chunk.ItemId);

                    var aiDocumentChunk = new AIDocumentChunkContext
                    {
                        ChunkId = chunk.ItemId,
                        DocumentId = aiDocument.ItemId,
                        Content = chunk.Content,
                        FileName = aiDocument.FileName,
                        ReferenceId = aiDocument.ReferenceId,
                        ReferenceType = aiDocument.ReferenceType,
                        ChunkIndex = chunk.Index,
                        Embedding = chunk.Embedding,
                    };

                    var buildContext = new BuildDocumentIndexContext(documentIndex, aiDocumentChunk, [chunk.ItemId], documentIndexManager.GetContentIndexSettings())
                    {
                        AdditionalProperties = new Dictionary<string, object>
                        {
                            { nameof(IndexProfile), indexProfile },
                        }
                    };

                    await documentIndexHandlers.InvokeAsync((handler, ctx) => handler.BuildIndexAsync(ctx), buildContext, logger);

                    chunkDocuments.Add(documentIndex);
                }
            }

            if (chunkDocuments.Count > 0)
            {
                await documentIndexManager.AddOrUpdateDocumentsAsync(indexProfile, chunkDocuments);
            }
        }
    }

    private static async Task RemoveDocumentChunksAsync(ShellScope scope, List<string> chunkIds)
    {
        var services = scope.ServiceProvider;
        var indexStore = services.GetRequiredService<IIndexProfileStore>();
        var indexProfiles = await indexStore.GetByTypeAsync(AIConstants.AIDocumentsIndexingTaskType);

        if (!indexProfiles.Any())
        {
            return;
        }

        foreach (var indexProfile in indexProfiles)
        {
            var documentIndexManager = services.GetKeyedService<IDocumentIndexManager>(indexProfile.ProviderName);

            if (documentIndexManager == null)
            {
                continue;
            }

            await documentIndexManager.DeleteDocumentsAsync(indexProfile, chunkIds);
        }
    }
}
