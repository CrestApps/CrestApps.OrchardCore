using CrestApps.OrchardCore.AI.Chat.Interactions.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundJobs;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Handlers;

public sealed class ChatInteractionIndexProfileHandler : IndexProfileHandlerBase
{
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
