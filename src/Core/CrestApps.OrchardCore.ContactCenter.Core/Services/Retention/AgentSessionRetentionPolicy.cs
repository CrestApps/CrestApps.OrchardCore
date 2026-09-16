using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;

/// <summary>
/// Purges agent sessions by their last heartbeat regardless of whether they still claim to be online, so
/// a session abandoned by a crashed node is collected instead of remaining online forever.
/// </summary>
public sealed class AgentSessionRetentionPolicy : ContactCenterRetentionPolicyBase<AgentSession, AgentSessionIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentSessionRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session used to find expired records.</param>
    /// <param name="agentSessionStore">The agent session store.</param>
    public AgentSessionRetentionPolicy(
        ISession session,
        IAgentSessionStore agentSessionStore)
        : base(session, agentSessionStore)
    {
    }

    /// <inheritdoc/>
    public override string EntityName => "AgentSession";

    /// <inheritdoc/>
    protected override double GetRetentionDays(ContactCenterRetentionOptions options) => options.AgentSessionRetentionDays;

    /// <inheritdoc/>
    protected override Expression<Func<AgentSessionIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => index.LastHeartbeatUtc != null && index.LastHeartbeatUtc < cutoffUtc;
}
