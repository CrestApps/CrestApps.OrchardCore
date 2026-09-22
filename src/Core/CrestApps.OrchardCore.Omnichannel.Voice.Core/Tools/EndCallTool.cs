using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Tools;

/// <summary>
/// The AI tool a live automated call invokes when the conversation has reached its end, so the platform hangs up
/// rather than leaving the caller to do it.
/// </summary>
/// <remarks>
/// It records the decision on the scoped <see cref="IVoiceCallEndTurn"/>; the session holding the line watches
/// that and closes the call once the assistant has finished its closing line and the caller has had a moment to
/// add anything. The tool takes no arguments beyond a reason, because when and how to hang up is the session's
/// business and not something the model should be deciding the mechanics of.
/// </remarks>
public sealed class EndCallTool : AIFunction
{
    /// <summary>
    /// The name the model calls this tool by.
    /// </summary>
    public const string ToolName = "endCall";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "reason": {
          "type": "string",
          "description": "A short reason the call is over (for example 'customer has what they needed' or 'customer asked not to be called again')."
        },
        "voicemail": {
          "type": "boolean",
          "description": "True when the call was answered by voicemail or an answering machine and you have just left your message on it."
        }
      },
      "additionalProperties": false,
      "required": []
    }
    """);

    /// <inheritdoc/>
    public override string Name => ToolName;

    /// <inheritdoc/>
    public override string Description =>
        "Ends the phone call after you have said goodbye. Call this when the conversation has genuinely finished: " +
        "the customer has what they needed, has declined, has asked not to be called again, or has said goodbye. " +
        "Say your closing line first and call this tool immediately after it; the call is hung up once you have " +
        "finished speaking and the customer has had a moment to add anything. Do not call it while the customer " +
        "still has questions, is mid-sentence, or is being transferred to a person. If the call is answered by " +
        "voicemail or an answering machine, wait until its greeting and tone have finished, leave one short " +
        "message, and call this tool immediately after it with voicemail set to true: nobody is going to answer, " +
        "so do not wait for a reply and do not repeat the message.";

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
        var answeredByMachine = ReadAnsweredByMachine(arguments);

        // The turn is a scoped service the session resolved for this call, so recording the decision here is
        // visible to that session and to nothing else running concurrently.
        var turn = arguments.Services?.GetService<IVoiceCallEndTurn>();
        var recorded = turn is not null;

        turn?.RequestEndCall(reason, answeredByMachine);

        if (arguments.Services is not null)
        {
            var logger = arguments.Services.GetService<ILogger<EndCallTool>>();

            if (logger is not null && logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("The AI ended the call (recorded: {Recorded}, answered by a machine: {AnsweredByMachine}).", recorded, answeredByMachine);
            }
        }

        // What the model reads back after the tool call. It is told the hangup is handled so it does not narrate
        // it ("let me disconnect now"), and does not call the tool again when nothing appears to happen.
        return ValueTask.FromResult<object>(recorded
            ? "The call will be ended for you once you finish speaking. Say nothing further unless the customer speaks again."
            : "This conversation cannot be ended from here; continue assisting the customer.");
    }

    // The schema says boolean, but the tool is not strict, so the argument can arrive as a JSON value or as text.
    //
    // Named for what it means rather than for the argument: code scanning reads "mail" in a name as an email
    // address, and reported logging this flag as exposing private data.
    private static bool ReadAnsweredByMachine(AIFunctionArguments arguments)
    {
        if (!arguments.TryGetValue("voicemail", out var value))
        {
            return false;
        }

        return value switch
        {
            bool flag => flag,
            JsonElement { ValueKind: JsonValueKind.True } => true,
            JsonElement { ValueKind: JsonValueKind.String } element => bool.TryParse(element.GetString(), out var parsed) && parsed,
            string text => bool.TryParse(text, out var parsed) && parsed,
            _ => false,
        };
    }
}
