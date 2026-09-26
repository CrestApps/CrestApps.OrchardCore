using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// What an agent is offered decides what their phone shows and what Accept does.
/// </summary>
public sealed class AgentOfferKindHelperTests
{
    // A caller who asked to be called back is not on the line: the agent is shown who to call, and accepting dials them,
    // as a preview dial does. Offered as an incoming call, the agent's phone waited for a caller who was never coming.
    [Theory]
    [InlineData(ActivitySources.Callback, AgentOfferKind.PreviewDial)]
    [InlineData(ActivitySources.PreviewDial, AgentOfferKind.PreviewDial)]
    [InlineData(ActivitySources.Inbound, AgentOfferKind.InboundCall)]
    [InlineData(null, AgentOfferKind.InboundCall)]
    public void FromActivitySource_OffersAQueuedCallbackAsACallToPlace(string source, AgentOfferKind expected)
    {
        // Act
        var kind = AgentOfferKindHelper.FromActivitySource(source);

        // Assert
        Assert.Equal(expected, kind);
    }
}
