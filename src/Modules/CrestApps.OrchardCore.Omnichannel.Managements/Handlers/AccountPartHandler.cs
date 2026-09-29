using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Lists.Indexes;
using OrchardCore.Lists.Models;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Detaches an account's contacts and opportunities when the account is removed, so they keep working as records
/// of their own instead of pointing at an account that no longer exists.
/// </summary>
internal sealed class AccountPartHandler : ContentPartHandler<AccountPart>
{
    private const int BatchSize = 100;

    public override Task RemovedAsync(RemoveContentContext context, AccountPart part)
    {
        // Removing the published version alone leaves a draft behind; only a full removal ends the account.
        if (!context.NoActiveVersionLeft)
        {
            return Task.CompletedTask;
        }

        var accountId = context.ContentItem.ContentItemId;

        ShellScope.AddDeferredTask(scope => DetachChildrenAsync(scope, accountId));

        return Task.CompletedTask;
    }

    private static async Task DetachChildrenAsync(ShellScope scope, string accountId)
    {
        var session = scope.ServiceProvider.GetRequiredService<ISession>();
        var contentManager = scope.ServiceProvider.GetRequiredService<IContentManager>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AccountPartHandler>>();
        var detached = 0;

        // Every child is read up front: detaching one changes the very index being read, so paging the index while
        // detaching would skip children or revisit them.
        var childIds = (await session.QueryIndex<ContainedPartIndex>(index => index.ListContentItemId == accountId)
            .ListAsync())
            .Select(index => index.ContentItemId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var batch in childIds.Chunk(BatchSize))
        {
            // Each batch is loaded again after the previous commit, so its items are tracked by the session that
            // saves them.
            var children = await contentManager.GetAsync(batch, VersionOptions.Latest);

            foreach (var child in children)
            {
                if (!child.Has<ContainedPart>())
                {
                    continue;
                }

                ((System.Text.Json.Nodes.JsonObject)child.Content).Remove(nameof(ContainedPart));
                await contentManager.UpdateAsync(child);

                if (child.Published)
                {
                    await contentManager.PublishAsync(child);
                }

                detached++;
            }

            await session.SaveChangesAsync();
        }

        if (detached > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Detached {Count} content item(s) from the removed account '{AccountId}'.",
                detached,
                accountId);
        }
    }
}
