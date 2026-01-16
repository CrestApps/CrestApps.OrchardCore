using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundJobs;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;
using OrchardCore.Search.Elasticsearch.Core.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Handlers;

public sealed class ChatInteractionIndexProfileHandler : IndexProfileHandlerBase
{
    public override Task InitializingAsync(InitializingContext<IndexProfile> context)
    {
        if (!CanHandle(context.Model))
        {
            return Task.CompletedTask;
        }

        var metadata = context.Model.As<ElasticsearchDefaultQueryMetadata>();

        if (metadata.DefaultSearchFields is null || metadata.DefaultSearchFields.Length == 0)
        {
            metadata.DefaultSearchFields =
            [
                ChatInteractionsConstants.ColumnNames.ChunksText,
            ];

            context.Model.Put(metadata);
        }

        return Task.CompletedTask;
    }

    public override Task SynchronizedAsync(IndexProfileSynchronizedContext context)
    {
        if (!CanHandle(context.IndexProfile))
        {
            return Task.CompletedTask;
        }

        ShellScope.AddDeferredTask(async s =>
        {
            await HttpBackgroundJob.ExecuteAfterEndOfRequestAsync("Synchronize chat interaction index", scope =>
            {
                var indexProfileId = context.IndexProfile.Id;

                var indexingService = scope.ServiceProvider.GetRequiredService<ChatInteractionIndexingService>();

                return indexingService.ProcessRecordsAsync([indexProfileId]);
            });
        });

        return Task.CompletedTask;
    }

    private static bool CanHandle(IndexProfile indexProfile)
    {
        return string.Equals(ChatInteractionsConstants.IndexingTaskType, indexProfile.Type, StringComparison.OrdinalIgnoreCase);
    }
}
