using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;

namespace CrestApps.OrchardCore.Omnichannel.Automation;

/// <summary>
/// Everything one turn of an automated conversation works with.
/// </summary>
internal sealed class AutomatedConversationTurn
{
    public IAutomatedMessagingChannel Channel { get; init; }

    public IMessagingChannel MessagingChannel { get; init; }

    public OmnichannelActivity Activity { get; init; }

    public OmnichannelChannelEndpoint Endpoint { get; init; }

    public SubjectFlowSettings FlowSettings { get; init; }

    public AIProfile Profile { get; init; }

    public AIChatSession ChatSession { get; init; }

    public OmnichannelMessage ReplyTo { get; init; }
}
