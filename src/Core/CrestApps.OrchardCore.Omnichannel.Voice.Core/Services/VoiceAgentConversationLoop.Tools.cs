using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Tools;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// The tools a turn-based call offers the model on each turn.
/// </summary>
public sealed partial class VoiceAgentConversationLoop
{
    // The call's own tools, offered alongside the profile's. The automated conversation calls the completion service
    // directly rather than through the orchestrator, so the tools are attached to the context for this turn only:
    // the profile's tools, tool instances, MCP connections and agents come from the same registry the orchestrator
    // reads (see AIConversationTools), and the call's system tools are added after them. The profile's own tool list
    // skips system tools, which these are, so they could never be enabled through it.
    private Task AttachCallToolsAsync(AICompletionContext context, bool offerTransfer, CancellationToken cancellationToken)
    {
        // The end-call tool is offered on every turn, and the transfer tool only when this call has somewhere to
        // transfer to. Ending the call needs no such condition: the model can always be finished talking, and a
        // call it cannot end is one that ends when the customer works out that nobody is going to hang up.
        var systemTools = new List<ToolRegistryEntry>
        {
            AIConversationTools.SystemTool(EndCallTool.ToolName, "Ends the phone call once the conversation has genuinely finished."),
        };

        if (offerTransfer)
        {
            systemTools.Add(AIConversationTools.SystemTool(
                OmnichannelHandoffHelper.TransferToAgentToolName,
                "Transfers the current conversation to a live human agent."));
        }

        return AIConversationTools.AttachAsync(_toolRegistry, context, systemTools, cancellationToken);
    }
}
