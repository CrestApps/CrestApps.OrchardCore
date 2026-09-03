using System.Text;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Pure helpers for AI-to-agent handoff: the name of the <c>transfer_to_agent</c> tool the model invokes to
/// escalate, and the system-prompt guidance that tells the model when to escalate. Kept free of I/O so both the
/// SMS and voice automated conversation handlers share one contract and it is fully unit-testable.
/// </summary>
public static class OmnichannelHandoffHelper
{
    /// <summary>
    /// The registered name of the tool the model calls to hand the conversation to a live human agent. The
    /// model decides to escalate and invokes this tool; there is no text marker to parse.
    /// </summary>
    public const string TransferToAgentToolName = "transferToLiveAgent";

    /// <summary>
    /// Determines whether the subject flow both enables agent handoff and has a queue configured to receive it.
    /// A handoff request cannot be honored without a destination queue, so an enabled-but-unconfigured flow is
    /// treated as not eligible.
    /// </summary>
    /// <param name="flowSettings">The subject flow settings.</param>
    /// <returns><see langword="true"/> when handoff is enabled and a target queue is configured.</returns>
    public static bool IsHandoffEnabled(SubjectFlowSettings flowSettings)
        => flowSettings is { EnableAgentHandoff: true } &&
           !string.IsNullOrWhiteSpace(flowSettings.HandoffQueueId);

    /// <summary>
    /// Builds the system-prompt guidance that instructs the model when to escalate to a live agent by calling the
    /// <c>transfer_to_agent</c> tool, honoring the flow's trigger policy. Returns <see langword="null"/> when
    /// handoff is not eligible or no trigger is enabled, so the caller can append it unconditionally.
    /// </summary>
    /// <param name="flowSettings">The subject flow settings.</param>
    /// <returns>The guidance snippet, or <see langword="null"/> when handoff is not eligible or no trigger is enabled.</returns>
    public static string BuildHandoffInstructions(SubjectFlowSettings flowSettings)
    {
        if (!IsHandoffEnabled(flowSettings))
        {
            return null;
        }

        var triggers = new List<string>();

        if (flowSettings.HandoffOnUserRequest)
        {
            triggers.Add("the customer asks to speak to a human, a person, an agent, or a representative");
        }

        if (flowSettings.HandoffOnQualifiedLead)
        {
            triggers.Add("you have learned enough to know the customer is a good fit and is ready to talk to a specialist");
        }

        if (flowSettings.HandoffOnFrustration)
        {
            triggers.Add("the customer becomes frustrated, upset, or you are repeatedly unable to help with their request");
        }

        // Handoff is enabled and configured, but no trigger is selected: there is no condition under which the
        // model may escalate, so emit no guidance rather than inviting an unbounded escalation.
        if (triggers.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();

        builder.AppendLine("## Handing off to a human agent");
        builder.AppendLine();
        builder.AppendLine("A live agent is available. Hand the conversation off to a human when any of the following is true:");

        foreach (var trigger in triggers)
        {
            builder.Append("- ").AppendLine(trigger);
        }

        builder.AppendLine();
        builder.AppendLine(
            "To hand off, call the transfer_to_agent tool with a short reason. After calling it, reply with one warm " +
            "message telling the customer you are connecting them with a specialist who will continue the conversation; " +
            "do not promise a specific time. Only hand off when one of the conditions above is genuinely met.");

        return builder.ToString().TrimEnd();
    }
}
