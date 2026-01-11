using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Handlers;

public sealed class ChatInteractionHandler : CatalogEntryHandlerBase<ChatInteraction>
{
    private readonly Dictionary<string, ChatInteraction> _interactions = [];
    private readonly Dictionary<string, ChatInteraction> _removedInteractions = [];

    public override Task CreatedAsync(CreatedContext<ChatInteraction> context)
    {
        return AddTranscriptAsync(context.Model);
    }

    public override Task UpdatedAsync(UpdatedContext<ChatInteraction> context)
    {
        return AddTranscriptAsync(context.Model);
    }

    public override Task DeletedAsync(DeletedContext<ChatInteraction> context)
        => RemovedTranscriptAsync(context.Model);

    private Task AddTranscriptAsync(ChatInteraction interaction)
    {
        AddDeferredTask();

        _interactions[interaction.ItemId] = interaction;

        return Task.CompletedTask;
    }

    private Task RemovedTranscriptAsync(ChatInteraction interaction)
    {
        AddDeferredTask();

        // Store the last content item in the request.
        _removedInteractions[interaction.ItemId] = interaction;

        return Task.CompletedTask;
    }

    private bool _taskAdded;

    private void AddDeferredTask()
    {
        if (_taskAdded)
        {
            return;
        }

        _taskAdded = true;

        // Using a local variable prevents the lambda from holding a ref on this scoped service.
        var interactions = _interactions;
        var removedTranscripts = _removedInteractions;

        ShellScope.AddDeferredTask(scope => IndexAsync(scope, interactions.Values, removedTranscripts.Values));
    }

    private static async Task IndexAsync(ShellScope scope, IEnumerable<ChatInteraction> interactions, IEnumerable<ChatInteraction> removedInteractions)
    {
        if (!interactions.Any() && !removedInteractions.Any())
        {
            return;
        }

        var services = scope.ServiceProvider;

        var indexStore = services.GetRequiredService<IIndexProfileStore>();

        var indexProfiles = await indexStore.GetByTypeAsync(ChatInteractionsConstants.IndexingTaskType);

        if (!indexProfiles.Any())
        {
            return;
        }

        var documentIndexHandlers = services.GetServices<IDocumentIndexHandler>();

        var logger = services.GetRequiredService<ILogger<ChatInteractionHandler>>();

        var interactionIds = new List<string>();

        foreach (var interaction in interactions)
        {
            for (var i = 0; i <= interaction.DocumentIndex; i++)
            {
                interactionIds.Add($"{interaction.ItemId}_{i}");
            }
        }

        foreach (var indexProfile in indexProfiles)
        {
            var documentIndexManager = services.GetKeyedService<IDocumentIndexManager>(indexProfile.ProviderName);

            if (documentIndexManager == null)
            {
                logger.LogWarning("No document index manager found for provider '{ProviderName}'.", indexProfile.ProviderName);

                continue;
            }

            var documents = new List<DocumentIndex>();

            foreach (var interaction in interactions)
            {
                foreach (var doc in interaction.Documents)
                {
                    var document = new DocumentIndex(doc.DocumentId);

                    var buildIndexContext = new BuildDocumentIndexContext(document, doc, [doc.DocumentId], documentIndexManager.GetContentIndexSettings())
                    {
                        AdditionalProperties = new Dictionary<string, object>
                        {
                            { nameof(IndexProfile), indexProfile },
                            { "Interaction", interaction },
                        }
                    };

                    await documentIndexHandlers.InvokeAsync(x => x.BuildIndexAsync(buildIndexContext), logger);

                    documents.Add(document);
                }
            }

            // Delete all of the documents that we'll be updating in this scope.
            await documentIndexManager.DeleteDocumentsAsync(indexProfile, interactionIds);

            if (documents.Count > 0)
            {
                // Update all of the documents that were updated in this scope.
                await documentIndexManager.AddOrUpdateDocumentsAsync(indexProfile, documents);
            }

            var removedIds = new List<string>();

            foreach (var interaction in removedInteractions)
            {
                for (var i = 0; i < interaction.DocumentIndex; i++)
                {
                    removedIds.Add($"{interaction.ItemId}_{i}");
                }
            }

            // At the end of the indexing, we remove the documents that were removed in the same scope.
            await documentIndexManager.DeleteDocumentsAsync(indexProfile, removedIds);
        }
    }
}
