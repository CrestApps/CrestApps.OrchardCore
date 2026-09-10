using CrestApps.OrchardCore.Asterisk.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Asterisk.Indexes;

/// <summary>
/// Maps <see cref="AsteriskChannelTenantBinding"/> documents to the <see cref="AsteriskChannelTenantBindingIndex"/>.
/// </summary>
public sealed class AsteriskChannelTenantBindingIndexProvider : IndexProvider<AsteriskChannelTenantBinding>
{
    /// <inheritdoc/>
    public override void Describe(DescribeContext<AsteriskChannelTenantBinding> context)
    {
        context
            .For<AsteriskChannelTenantBindingIndex>()
            .Map(binding => new AsteriskChannelTenantBindingIndex
            {
                ChannelId = binding.ChannelId,
                ProviderName = binding.ProviderName,
                InteractionId = binding.InteractionId,
                PeerChannelId = binding.PeerChannelId,
            });
    }
}
