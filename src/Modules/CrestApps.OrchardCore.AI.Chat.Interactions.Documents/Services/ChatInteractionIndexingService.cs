using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Services;

public sealed class ChatInteractionIndexingService
{
    private readonly ILogger _logger;

    private readonly IIndexProfileStore _indexProfileStore;

    private readonly IIndexingTaskManager _indexingTaskManager;
    private readonly ISourceCatalog<ChatInteraction> _sourceCatalog;
    private readonly IEnumerable<IDocumentIndexHandler> _documentIndexHandlers;

    private readonly IServiceProvider _serviceProvider;
    private Dictionary<string, ChatInteraction> _interactions = [];

    private const int _batchSize = 100;

    public ChatInteractionIndexingService(
        IIndexProfileStore indexProfileStore,
        IIndexingTaskManager indexingTaskManager,
        ISourceCatalog<ChatInteraction> sourceCatalog,
        IEnumerable<IDocumentIndexHandler> documentIndexHandlers,
        IServiceProvider serviceProvider,
        ILogger<ChatInteractionIndexingService> logger)
    {
        _indexProfileStore = indexProfileStore;
        _indexingTaskManager = indexingTaskManager;
        _sourceCatalog = sourceCatalog;
        _documentIndexHandlers = documentIndexHandlers;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task ProcessRecordsForAllIndexesAsync()
    {
        await ProcessRecordsAsync(await _indexProfileStore.GetByTypeAsync(ChatInteractionsConstants.IndexingTaskType));
    }

    public async Task ProcessRecordsAsync(IEnumerable<string> indexIds)
    {
        ArgumentNullException.ThrowIfNull(indexIds);

        if (indexIds.Any())
        {
            await ProcessRecordsAsync((await _indexProfileStore.GetByTypeAsync(ChatInteractionsConstants.IndexingTaskType)).Where((IndexProfile x) => indexIds.Contains(x.Id)));
        }
    }

    private async Task ProcessRecordsAsync(IEnumerable<IndexProfile> indexProfiles)
    {
        if (!indexProfiles.Any())
        {
            return;
        }

        var tracker = new Dictionary<string, IndexProfileEntryContext>();
        var documentIndexManagers = new Dictionary<string, IDocumentIndexManager>();
        var indexManagers = new Dictionary<string, IIndexManager>();
        var lastTaskId = long.MaxValue;

        foreach (var indexProfile in indexProfiles)
        {
            if (indexProfile.Type != ChatInteractionsConstants.IndexingTaskType)
            {
                continue;
            }

            if (!documentIndexManagers.TryGetValue(indexProfile.ProviderName, out var documentIndexManager))
            {
                documentIndexManager = _serviceProvider.GetKeyedService<IDocumentIndexManager>(indexProfile.ProviderName);

                if (documentIndexManager == null)
                {
                    _logger.LogWarning("Unable to find an implementation of {Implementation} for the provider '{ProviderName}'", "IDocumentIndexManager", indexProfile.ProviderName);

                    continue;
                }

                documentIndexManagers.Add(indexProfile.ProviderName, documentIndexManager);
            }

            if (!indexManagers.TryGetValue(indexProfile.ProviderName, out var value))
            {
                value = _serviceProvider.GetKeyedService<IIndexManager>(indexProfile.ProviderName);

                if (value == null)
                {
                    _logger.LogWarning("Unable to find an implementation of {Implementation} for the provider '{ProviderName}'", "IIndexManager", indexProfile.ProviderName);

                    continue;
                }

                indexManagers.Add(indexProfile.ProviderName, value);
            }

            if (!await value.ExistsAsync(indexProfile.IndexFullName))
            {
                _logger.LogWarning("The index '{IndexName}' does not exist for the provider '{ProviderName}'.", indexProfile.IndexName, indexProfile.ProviderName);
            }
            else
            {
                var num = await documentIndexManager.GetLastTaskIdAsync(indexProfile);
                lastTaskId = Math.Min(lastTaskId, num);
                tracker.Add(indexProfile.Id, new IndexProfileEntryContext(indexProfile, documentIndexManager, num));
                documentIndexManager = null;
            }
        }

        if (tracker.Count == 0)
        {
            return;
        }

        var tasks = new List<RecordIndexingTask>();

        while (true)
        {
            tasks = (await _indexingTaskManager.GetIndexingTasksAsync(lastTaskId, _batchSize, ChatInteractionsConstants.IndexingTaskType)).ToList();
            if (tasks.Count == 0)
            {
                break;
            }

            var updatedDocumentsByIndex = tracker.Values.ToDictionary(x => x.IndexProfile.Id, b => new List<DocumentIndex>());

            var removedDocumentsByIndex = tracker.Values.ToDictionary(x => x.IndexProfile.Id, _ => new List<string>());

            await BeforeProcessingTasksAsync(tasks);

            foreach (var entry in tracker.Values)
            {
                foreach (var task in tasks)
                {
                    if (task.Id < entry.LastTaskId)
                    {
                        continue;
                    }

                    if (!_interactions.TryGetValue(task.RecordId, out var interaction))
                    {
                        // If the transcript is not found, we can skip indexing this entry.
                        continue;
                    }

                    foreach (var doc in interaction.Documents)
                    {
                        var document = new DocumentIndex(doc.DocumentId);

                        var buildIndexContext = new BuildDocumentIndexContext(document, doc, [doc.DocumentId], entry.DocumentIndexManager.GetContentIndexSettings())
                        {
                            AdditionalProperties = new Dictionary<string, object>
                            {
                                { nameof(IndexProfile), entry.IndexProfile },
                                { "Interaction", interaction },
                            }
                        };

                        await _documentIndexHandlers.InvokeAsync((x, ctx) => x.BuildIndexAsync(ctx), buildIndexContext, _logger);

                        updatedDocumentsByIndex[entry.IndexProfile.Id].Add(buildIndexContext.DocumentIndex);
                    }

                    for (var i = 0; i < interaction.DocumentIndex; i++)
                    {
                        removedDocumentsByIndex[entry.IndexProfile.Id].Add($"{interaction.ItemId}_{i}");
                    }
                }
            }

            lastTaskId = tasks.Last().Id;

            foreach (var indexEntry in updatedDocumentsByIndex)
            {
                if (indexEntry.Value.Count != 0)
                {
                    var entry = tracker[indexEntry.Key];

                    if (removedDocumentsByIndex.TryGetValue(indexEntry.Key, out var documentIds) && documentIds.Count > 0)
                    {
                        await entry.DocumentIndexManager.DeleteDocumentsAsync(entry.IndexProfile, documentIds);
                    }

                    if (await entry.DocumentIndexManager.AddOrUpdateDocumentsAsync(entry.IndexProfile, indexEntry.Value))
                    {
                        await entry.DocumentIndexManager.SetLastTaskIdAsync(entry.IndexProfile, lastTaskId);
                    }
                }
            }
        }
    }

    private async Task BeforeProcessingTasksAsync(IEnumerable<RecordIndexingTask> tasks)
    {
        var interactionIds = tasks
            .Where(x => x.Type == RecordIndexingTaskTypes.Update)
            .Select(x => x.RecordId)
            .Distinct()
            .ToArray();

        _interactions = (await _sourceCatalog.GetAsync(interactionIds)).ToDictionary(x => x.ItemId);
    }
}
