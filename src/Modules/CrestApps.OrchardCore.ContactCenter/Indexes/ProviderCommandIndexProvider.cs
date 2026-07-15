using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="ProviderCommand"/> documents to the <see cref="ProviderCommandIndex"/>.
/// </summary>
public sealed class ProviderCommandIndexProvider : IndexProvider<ProviderCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderCommandIndexProvider"/> class.
    /// </summary>
    public ProviderCommandIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ProviderCommand> context)
    {
        context
            .For<ProviderCommandIndex>()
            .Map(command => new ProviderCommandIndex
            {
                ItemId = command.ItemId,
                CommandId = command.CommandId,
                ProviderName = command.ProviderName,
                Status = command.Status,
                FenceToken = command.FenceToken,
                InteractionId = command.InteractionId,
                NextAttemptUtc = command.NextAttemptUtc,
                LeaseExpiresUtc = command.LeaseExpiresUtc,
            });
    }
}
