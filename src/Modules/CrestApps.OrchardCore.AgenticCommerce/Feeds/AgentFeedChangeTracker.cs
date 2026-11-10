using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// Collects the products that changed in a unit of work, such as a publish or a stock change, and pushes them to the
/// ACP feed APIs once the unit of work is done, in a deferred task.
/// </summary>
public sealed class AgentFeedChangeTracker
{
    private readonly HashSet<string> _contentItemIds = new(StringComparer.Ordinal);

    /// <summary>
    /// Adds a product that changed. The first one schedules the push.
    /// </summary>
    /// <param name="contentItemId">The content item id of the product.</param>
    public void Add(string contentItemId)
    {
        if (string.IsNullOrEmpty(contentItemId) || !_contentItemIds.Add(contentItemId) || _contentItemIds.Count > 1)
        {
            return;
        }

        var ids = _contentItemIds;

        ShellScope.AddDeferredTask(async scope =>
        {
            var pusher = scope.ServiceProvider.GetRequiredService<AcpFeedPusher>();

            if (await pusher.HasTargetsAsync())
            {
                await pusher.PushProductsAsync([.. ids]);
            }
        });
    }
}
