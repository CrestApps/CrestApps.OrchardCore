using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="CallSession"/> documents to the <see cref="CallSessionIndex"/>.
/// </summary>
public sealed class CallSessionIndexProvider : IndexProvider<CallSession>
{
    private readonly IProviderIdentityResolver _providerIdentityResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallSessionIndexProvider"/> class.
    /// </summary>
    /// <param name="providerIdentityResolver">The resolver used to canonicalize provider aliases so legacy documents cannot recreate alias index values on reindex.</param>
    public CallSessionIndexProvider(IProviderIdentityResolver providerIdentityResolver)
    {
        _providerIdentityResolver = providerIdentityResolver;
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<CallSession> context)
    {
        context
            .For<CallSessionIndex>()
            .Map(session =>
            {
                // Canonicalize the provider identity while mapping so that a legacy document stored with an
                // alias (for example "Default Asterisk") always produces the canonical index value and
                // cannot recreate an alias-scoped provider-call claim on reindex.
                var providerName = _providerIdentityResolver.Canonicalize(session.ProviderName);

                return new CallSessionIndex
                {
                    ItemId = session.ItemId,
                    InteractionId = session.InteractionId,
                    ActivityItemId = session.ActivityItemId,
                    ProviderName = providerName,
                    ProviderCallId = session.ProviderCallId,
                    ProviderCallClaimKey = ContactCenterClaimKeys.BuildProviderCallClaim(
                        providerName,
                        session.ProviderCallId,
                        session.ItemId),
                    State = session.State,
                    AgentId = session.AgentId,
                    QueueId = session.QueueId,
                    CreatedUtc = session.CreatedUtc,
                    EndedUtc = session.EndedUtc,
                };
            });
    }
}
