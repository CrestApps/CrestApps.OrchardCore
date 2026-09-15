using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

/// <summary>
/// Stands in for the first-response SLA in tests that are not about it. A queue with no target has no
/// deadline, which is exactly the situation a test that never configures one is describing.
/// </summary>
internal sealed class NoOpSmsFirstResponseSlaService : ISmsFirstResponseSlaService
{
    public Task ApplyFirstResponseTargetAsync(SmsConversation conversation, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public void MarkResponded(SmsConversation conversation)
    {
    }

    public Task<int> EscalateOverdueAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}
