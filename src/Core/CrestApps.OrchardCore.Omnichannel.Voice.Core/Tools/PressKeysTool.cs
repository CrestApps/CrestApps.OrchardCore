using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Tools;

/// <summary>
/// Lets the AI press keys on a phone call, to get through the phone menu of the business it called or to enter an
/// extension.
/// </summary>
public sealed class PressKeysTool : AIFunction
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "pressKeys";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "keys": {
          "type": "string",
          "description": "The keys to press, in order: digits 0 to 9, * and #. Use w for a half-second pause and W for a one-second pause, for example '1' or 'W204#'."
        }
      },
      "additionalProperties": false,
      "required": [ "keys" ]
    }
    """);

    /// <inheritdoc/>
    public override string Name => ToolName;

    /// <inheritdoc/>
    public override string Description =>
        "Presses keys on the phone keypad, as if you pressed them on a handset. Use it when an automated phone menu " +
        "asks you to press a number, or to enter an extension. Listen to the whole menu first, then press the key for " +
        "the option you need, and wait for what the menu says next. Never press keys while a person is talking to you.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <summary>
    /// Keeps the characters a keypad has, and drops everything else the model may have added.
    /// </summary>
    /// <param name="keys">The keys the model asked for.</param>
    /// <returns>The keys to play.</returns>
    internal static string Sanitize(string keys)
        => new((keys ?? string.Empty).Where(key => char.IsAsciiDigit(key) || key is '*' or '#' or 'w' or 'W').ToArray());

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        arguments.TryGetFirstString("keys", out var requested);
        var keys = Sanitize(requested);

        if (keys.Length == 0)
        {
            return "No keys were pressed: give the keys as digits, * or #.";
        }

        var services = arguments.Services;
        var activityId = AIConversationScope.Current?.ActivityId;
        var activityStore = services?.GetService<IOmnichannelActivityStore>();

        if (string.IsNullOrEmpty(activityId) || activityStore is null)
        {
            return "Keys can only be pressed on a phone call.";
        }

        var activity = await activityStore.FindByIdAsync(activityId, cancellationToken);

        if (activity is null || !activity.TryGet<AILiveCall>(out var liveCall))
        {
            return "Keys can only be pressed on a phone call.";
        }

        var resolver = services.GetService<IVoiceAgentMediaProviderResolver>();
        var media = resolver?.Get(liveCall.ProviderName) ?? resolver?.GetDefault();

        if (media is null || !await media.SendDigitsAsync(liveCall.ProviderCallId, keys, cancellationToken))
        {
            return "The keys could not be pressed on this call.";
        }

        var logger = services.GetService<ILogger<PressKeysTool>>();

        if (logger is not null && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("The AI pressed keys '{Keys}' on call activity '{ActivityId}'.", keys, activityId.SanitizeLogValue());
        }

        return $"Pressed {keys}. Wait for the menu to answer before you press anything else.";
    }
}
