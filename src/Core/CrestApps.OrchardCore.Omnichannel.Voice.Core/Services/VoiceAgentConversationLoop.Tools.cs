using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// The tools a turn-based call offers the model on each turn.
/// </summary>
public sealed partial class VoiceAgentConversationLoop
{
    // Attaches the transfer-to-agent tool to this single completion. The automated conversation calls the completion
    // service directly rather than through the tool orchestrator, so the scoped-tool key the function-invocation
    // service handler reads is otherwise never populated and no tools reach the model. We register the transfer tool
    // as a scoped system-tool entry for this turn only (the context is built per turn and never persisted). Enabling
    // it through the profile's tool-name list does not work: the profile tool provider reads the names snapshotted
    // when the context was built and, either way, skips system tools — which the transfer tool is.
    private static void AttachCallTools(AICompletionContext context, bool offerTransfer)
    {
        // The end-call tool is offered on every turn, and the transfer tool only when this call has somewhere to
        // transfer to. Ending the call needs no such condition: the model can always be finished talking, and a
        // call it cannot end is one that ends when the customer works out that nobody is going to hang up.
        var entries = new List<ToolRegistryEntry>
        {
            new()
            {
                Id = EndCallTool.ToolName,
                Name = EndCallTool.ToolName,
                Description = "Ends the phone call once the conversation has genuinely finished.",
                Source = ToolRegistryEntrySource.System,
                CreateAsync = serviceProvider => ValueTask.FromResult(
                    serviceProvider.GetKeyedService<AITool>(EndCallTool.ToolName)),
            },
        };

        if (offerTransfer)
        {
            entries.Add(new ToolRegistryEntry
            {
                Id = OmnichannelHandoffHelper.TransferToAgentToolName,
                Name = OmnichannelHandoffHelper.TransferToAgentToolName,
                Description = "Transfers the current conversation to a live human agent.",
                Source = ToolRegistryEntrySource.System,
                CreateAsync = serviceProvider => ValueTask.FromResult(
                    serviceProvider.GetKeyedService<AITool>(OmnichannelHandoffHelper.TransferToAgentToolName)),
            });
        }

        context.AdditionalProperties[FunctionInvocationAICompletionServiceHandler.ScopedEntriesKey] = entries;
    }
}
