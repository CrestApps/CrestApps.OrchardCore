using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps each <see cref="CallSession"/> to one <see cref="CallSessionLegIndex"/> row per leg the provider has an
/// identifier for.
/// </summary>
public sealed class CallSessionLegIndexProvider : IndexProvider<CallSession>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallSessionLegIndexProvider"/> class.
    /// </summary>
    public CallSessionLegIndexProvider()
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<CallSession> context)
    {
        context
            .For<CallSessionLegIndex>()
            .Map(session => session.Legs
                .Where(leg => !string.IsNullOrEmpty(leg.ProviderLegId))
                .Select(leg => new CallSessionLegIndex
                {
                    ItemId = session.ItemId,
                    ProviderLegId = leg.ProviderLegId,
                    Role = leg.Role,
                    AgentId = leg.AgentId,
                    InteractionId = session.InteractionId,
                    StartedUtc = leg.StartedUtc,
                }));
    }
}
