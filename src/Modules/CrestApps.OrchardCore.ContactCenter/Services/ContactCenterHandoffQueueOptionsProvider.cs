using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// The Contact Center implementation of <see cref="IHandoffQueueOptionsProvider"/>. Offers the enabled queues as
/// handoff destinations for the subject AI-settings editor.
/// </summary>
public sealed class ContactCenterHandoffQueueOptionsProvider : IHandoffQueueOptionsProvider
{
    private readonly IActivityQueueManager _queueManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterHandoffQueueOptionsProvider"/> class.
    /// </summary>
    /// <param name="queueManager">The activity queue manager.</param>
    public ContactCenterHandoffQueueOptionsProvider(IActivityQueueManager queueManager)
    {
        _queueManager = queueManager;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<HandoffQueueOption>> GetQueuesAsync(CancellationToken cancellationToken = default)
    {
        var queues = await _queueManager.GetEnabledAsync(cancellationToken);

        return queues
            .OrderBy(queue => queue.Name, StringComparer.OrdinalIgnoreCase)
            .Select(queue => new HandoffQueueOption
            {
                Id = queue.ItemId,
                Name = queue.Name,
            })
            .ToArray();
    }
}
