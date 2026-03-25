using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Documents.Services;
using CrestApps.OrchardCore.AI.Documents.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.Support;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Documents.Drivers;

internal sealed class AIProfileDocumentsDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly ISiteService _siteService;
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAIDocumentStore _documentStore;
    private readonly IAIDocumentChunkStore _chunkStore;
    private readonly IAIDocumentProcessingService _documentProcessingService;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IOptions<ChatDocumentsOptions> _extractorOptions;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public AIProfileDocumentsDisplayDriver(
        ISiteService siteService,
        IIndexProfileStore indexProfileStore,
        IServiceProvider serviceProvider,
        IAIDocumentStore documentStore,
        IAIDocumentChunkStore chunkStore,
        IAIDocumentProcessingService documentProcessingService,
        IAIDeploymentManager deploymentManager,
        IOptions<ChatDocumentsOptions> extractorOptions,
        ILogger<AIProfileDocumentsDisplayDriver> logger,
        IStringLocalizer<AIProfileDocumentsDisplayDriver> stringLocalizer)
    {
        _siteService = siteService;
        _indexProfileStore = indexProfileStore;
        _serviceProvider = serviceProvider;
        _documentStore = documentStore;
        _chunkStore = chunkStore;
        _documentProcessingService = documentProcessingService;
        _deploymentManager = deploymentManager;
        _extractorOptions = extractorOptions;
        _logger = logger;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditAIProfileDocumentsViewModel>("AIProfileDocuments_Edit", async model =>
        {
            model.ProfileId = profile.ItemId;

            var documentsMetadata = profile.As<DocumentsMetadata>();
            model.Documents = documentsMetadata.Documents ?? [];
            model.TopN = documentsMetadata.DocumentTopN ?? 3;

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
        }).Location("Content:5#Documents:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditAIProfileDocumentsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var documentsMetadata = profile.As<DocumentsMetadata>();
        documentsMetadata.DocumentTopN = model.TopN > 0 ? model.TopN : 3;
        documentsMetadata.Documents ??= [];

        if (context.Updater.ModelState.IsValid)
        {
            // Handle document removals.
            if (model.RemovedDocumentIds != null && model.RemovedDocumentIds.Length > 0)
            {
                var chunkIdsToRemove = new List<string>();

                foreach (var documentId in model.RemovedDocumentIds)
                {
                    if (string.IsNullOrEmpty(documentId))
                    {
                        continue;
                    }

                    var document = await _documentStore.FindByIdAsync(documentId);

                    if (document != null)
                    {
                        // Collect chunk IDs for vector index removal.
                        var chunks = await _chunkStore.GetChunksByAIDocumentIdAsync(document.ItemId);
                        foreach (var chunk in chunks)
                        {
                            chunkIdsToRemove.Add(chunk.ItemId);
                        }

                        await _chunkStore.DeleteByDocumentIdAsync(document.ItemId);
                        await _documentStore.DeleteAsync(document);
                    }

                    var docInfo = documentsMetadata.Documents.FirstOrDefault(d => d.DocumentId == documentId);

                    if (docInfo != null)
                    {
                        documentsMetadata.Documents.Remove(docInfo);
                    }
                }

                // Schedule removal of chunks from the vector index.
                if (chunkIdsToRemove.Count > 0)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Scheduling removal of {ChunkCount} chunk(s) from vector index for profile '{ProfileId}'.",
                            chunkIdsToRemove.Count, profile.ItemId);
                    }

                    ShellScope.AddDeferredTask(scope => RemoveDocumentChunksAsync(scope, chunkIdsToRemove));
                }
            }

            // Handle file uploads.
            if (model.Files != null && model.Files.Length > 0)
            {
                var deployment = await ResolveDeploymentAsync(profile);
                var connectionName = deployment?.ConnectionName;
                var embeddingGenerator = await _documentProcessingService.CreateEmbeddingGeneratorAsync(deployment?.ClientName, connectionName);
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
                        context.Updater.ModelState.AddModelError(
                            Prefix + "." + nameof(model.Files),
                            S["File type '{0}' is not supported for AI Profile documents. Only text-based files are allowed.", extension]);
                        continue;
                    }

                    try
                    {
                        var result = await _documentProcessingService.ProcessFileAsync(
                            file,
                            profile.ItemId,
                            AIConstants.DocumentReferenceTypes.Profile,
                            embeddingGenerator);

                        if (!result.Success)
                        {
                            context.Updater.ModelState.AddModelError(
                                Prefix + "." + nameof(model.Files),
                                S["{0}: {1}", file.FileName, result.Error]);
                            continue;
                        }

                        documentsMetadata.Documents.Add(result.DocumentInfo);
                        await _documentStore.CreateAsync(result.Document);

                        // Persist each chunk as a separate record.
                        foreach (var chunk in result.Chunks)
                        {
                            await _chunkStore.CreateAsync(chunk);
                        }

                        processedDocuments.Add(result.Document);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process file {FileName}", file.FileName.SanitizeLogValue());
                        context.Updater.ModelState.AddModelError(
                            Prefix + "." + nameof(model.Files),
                            S["Failed to process file '{0}'.", file.FileName]);
                    }
                }

                // Schedule vector indexing of processed document chunks as a deferred task.
                if (processedDocuments.Count > 0)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("Scheduling vector indexing for {DocCount} document(s) for profile '{ProfileId}'.",
                            processedDocuments.Count,
                            profile.ItemId);
                    }

                    var docs = processedDocuments.ToList();
                    ShellScope.AddDeferredTask(scope => IndexDocumentChunksAsync(scope, docs));
                }
            }
        }

        profile.Put(documentsMetadata);

        return Edit(profile, context);
    }

    private async Task<AIDeployment> ResolveDeploymentAsync(AIProfile profile)
    {
        return await _deploymentManager.ResolveOrDefaultAsync(
            AIDeploymentType.Chat,
            deploymentId: profile.ChatDeploymentId)
            ?? await _deploymentManager.ResolveOrDefaultAsync(
                AIDeploymentType.Utility,
                deploymentId: profile.UtilityDeploymentId);
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
        var logger = services.GetRequiredService<ILogger<AIProfileDocumentsDisplayDriver>>();

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
