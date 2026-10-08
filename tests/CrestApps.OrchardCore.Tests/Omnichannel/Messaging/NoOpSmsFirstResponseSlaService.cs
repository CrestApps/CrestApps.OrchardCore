using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// Stands in for the first-response SLA in tests that are not about it. A queue with no target has no
/// deadline, which is exactly the situation a test that never configures one is describing.
/// </summary>
internal sealed class NoOpSmsFirstResponseSlaService : IMessagingFirstResponseSlaService
{
    public Task ApplyFirstResponseTargetAsync(MessagingConversation conversation, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public void MarkResponded(MessagingConversation conversation)
    {
    }

    public Task<int> EscalateOverdueAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}
