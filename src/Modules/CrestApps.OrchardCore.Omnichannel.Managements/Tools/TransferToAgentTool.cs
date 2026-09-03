using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Tools;

/// <summary>
/// The AI tool an automated omnichannel conversation invokes to hand the customer to a live human agent. It
/// records the decision on the current <see cref="OmnichannelHandoffTurnContext"/>; the conversation handler
/// that ran the completion reads that decision and performs the channel-specific handoff (seed the SMS thread,
/// or seat the caller in a queue). The tool takes no channel or destination arguments — the handler owns that
/// context — so the model only has to decide "transfer now, for this reason".
/// </summary>
public sealed class TransferToAgentTool : AIFunction
{
    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "reason": {
          "type": "string",
          "description": "A short reason for transferring to a human (for example 'customer asked for a person')."
        }
      },
      "additionalProperties": false,
      "required": []
    }
    """);

    /// <inheritdoc/>
    public override string Name => OmnichannelHandoffHelper.TransferToAgentToolName;

    /// <inheritdoc/>
    public override string Description =>
        "Hands off (escalates/transfers) the current conversation to a live human agent. " +
        "Call this tool immediately whenever the customer asks to speak to a human, a person, a real agent, " +
        "a representative, or support staff, or otherwise clearly wants to stop talking to the automated " +
        "assistant and be connected to a live person. Also call it when the customer is frustrated and you " +
        "cannot help. Prefer calling this tool over refusing, deflecting, or telling the customer you can only " +
        "help yourself. Provide a short 'reason' describing why the handoff is needed.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        arguments.TryGetFirstString("reason", out var reason);

        var recorded = OmnichannelHandoffTurnContext.RequestHandoff(reason);

        if (arguments.Services is not null)
        {
            var logger = arguments.Services.GetService<ILogger<TransferToAgentTool>>();

            if (logger is not null && logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("The AI requested a live-agent transfer (recorded: {Recorded}).", recorded);
            }
        }

        // The message the model reads back after the tool call, so it composes a natural closing line to the customer.
        return ValueTask.FromResult<object>(recorded
            ? "The transfer to a live agent has been queued. Reply with a short, warm message telling the customer you are connecting them with a specialist."
            : "A live-agent transfer is not available in this context; continue assisting the customer.");
    }
}
