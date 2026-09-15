using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;

/// <summary>
/// Describes one page of the SMS inbox: who is asking, which threads they may see, which tab they are on, and
/// where in the ordered result the page starts. The store turns it into a single indexed query, so the inbox no
/// longer loads every conversation in the tenant and filters in memory.
/// </summary>
public sealed class SmsInboxQuery
{
    /// <summary>
    /// Gets or sets the identifier of the agent asking. Threads assigned to them, and personal threads they own,
    /// are always visible.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the queues the agent serves. Threads owned by one of them are visible unless another agent
    /// has already taken them.
    /// </summary>
    public IReadOnlyCollection<string> QueueIds { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the caller may see every thread in the tenant, which is what the
    /// supervisor permission grants.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the inbox tab being shown.
    /// </summary>
    public SmsInboxFilter Filter { get; set; } = SmsInboxFilter.All;

    /// <summary>
    /// Gets or sets how many conversations to skip before the page starts.
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// Gets or sets how many conversations the page holds. Zero or less means "no bound", which only the count
    /// query uses.
    /// </summary>
    public int Take { get; set; }
}
