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
        "still has questions, is mid-sentence, or is being transferred to a person, and never in the same turn as a " +
        "question you have just asked them: if you read details back to confirm them, wait for the customer to " +
        "confirm before you say goodbye. If the call is answered by " +
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
        var logger = arguments.Services?.GetService<ILogger<EndCallTool>>();

        // The assistant asked the customer something, or said it would read details back, and they have not
        // answered. A voicemail has nobody to answer, so it is never held. See VoiceConfirmation.
        if (!answeredByMachine && turn is not null && turn.TryHoldForAnswer())
        {
            if (logger is not null && logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("The AI asked to end the call while waiting on the customer's answer; the call is kept open for it.");
            }

            return ValueTask.FromResult<object>(
                "The call has NOT been ended. You asked the customer something, or said you would read details " +
                "back, and they have not answered yet. If you said you would read something back, read it now and " +
                "ask them to confirm. Then stop and wait for their answer. End the call only after they reply.");
        }

        var recorded = turn is not null;

        // The model already ended the call and has said nothing since the customer last spoke: this is the same
        // decision again, made in answer to the tool's own reply. It is not counted as a new request, and the model
        // is told plainly to stop -- told only to "say nothing further", one model ended the call twelve times running.
        if (recorded && !answeredByMachine && turn.EndCallAlreadyRequested)
        {
            if (logger is not null && logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("The AI asked again to end a call it had already ended; the repeat is ignored.");
            }

            return ValueTask.FromResult<object>(
                "The call is already ending. Do not call this tool again, and do not say anything more unless the " +
                "customer speaks.");
        }

        // Read before the request: no goodbye has been said since the customer last spoke -- they spoke last, or the
        // assistant's last line was something else ("let me wrap this up") -- so ending it now ends it without one.
        // A voicemail's message is its closing line.
        var closingLineOwed = recorded && !answeredByMachine && turn.ClosingLineOwed;

        turn?.RequestEndCall(reason, answeredByMachine);

        if (logger is not null && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "The AI ended the call (recorded: {Recorded}, answered by a machine: {AnsweredByMachine}, closing line owed: {ClosingLineOwed}).",
                recorded,
                answeredByMachine,
                closingLineOwed);
        }

        // What the model reads back after the tool call. It is told the hangup is handled so it does not narrate
        // it ("let me disconnect now"), and does not call the tool again when nothing appears to happen. A model that
        // ended the call without a word is told to say its goodbye: told only to say nothing further, it did, and
        // the customer heard their own "yes" answered by the line going dead.
        if (!recorded)
        {
            return ValueTask.FromResult<object>("This conversation cannot be ended from here; continue assisting the customer.");
        }

        return ValueTask.FromResult<object>(closingLineOwed
            ? "The call will be ended for you once you finish speaking, but you have not said goodbye yet. " +
              "Say one short closing line now -- thank them and say goodbye -- and nothing else. Do not " +
              "ask anything and do not mention hanging up."
            : "The call will be ended for you once you finish speaking. Say nothing further unless the customer speaks again.");
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
