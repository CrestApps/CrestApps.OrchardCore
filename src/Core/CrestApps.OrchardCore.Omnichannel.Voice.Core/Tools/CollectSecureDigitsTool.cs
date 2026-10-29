using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Tools;

/// <summary>
/// Makes the caller's keypad private, so a card, account or identity number they type goes to secure data capture
/// instead of the AI. The AI is told when the entry was saved, with its masked value, and never sees the digits.
/// </summary>
public sealed class CollectSecureDigitsTool : AIFunction
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "collectSecureDigits";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "field": {
          "type": "string",
          "enum": [ "CreditCardNumber", "CardExpiry", "CardSecurityCode", "BankAccountNumber", "NationalId", "Custom" ],
          "description": "What the caller is about to type."
        }
      },
      "additionalProperties": false,
      "required": [ "field" ]
    }
    """);

    /// <inheritdoc/>
    public override string Name => ToolName;

    /// <inheritdoc/>
    public override string Description =>
        "Makes the caller's phone keypad private before they type a card number, expiry date, security code, bank " +
        "account number or identity number. Call it first, then ask the caller to type the number on their keypad " +
        "followed by the pound key. You will be told when it was saved; you never hear the digits. Never ask for these " +
        "numbers to be read aloud.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        arguments.TryGetFirstString("field", out var requested);

        if (!Enum.TryParse<SecureCaptureField>(requested, ignoreCase: true, out var field))
        {
            field = SecureCaptureField.Custom;
        }

        var services = arguments.Services;
        var activityId = AIConversationScope.Current?.ActivityId;
        var activityStore = services?.GetService<IOmnichannelActivityStore>();

        if (string.IsNullOrEmpty(activityId) || activityStore is null)
        {
            return "Secure keypad entry works only on a phone call.";
        }

        if (services.GetService<ISecureCaptureTokenSink>() is null)
        {
            return "Secure keypad entry is not available. Do not ask for the number; offer another way to pay or to verify.";
        }

        var activity = await activityStore.FindByIdAsync(activityId, cancellationToken);

        if (activity is null || !activity.Has<AILiveCall>())
        {
            return "Secure keypad entry works only on a phone call.";
        }

        SecureKeypadCaptures.Start(activityId, field, services.GetRequiredService<IClock>().UtcNow);

        var logger = services.GetService<ILogger<CollectSecureDigitsTool>>();

        if (logger is not null && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("The keypad on AI call activity '{ActivityId}' is private for a {Field} entry.", activityId.SanitizeLogValue(), field);
        }

        return $"The keypad is private now. Ask the caller to type the {SecureKeypadEntries.Describe(field)} on their keypad and press the pound key when done, or star to cancel. Wait quietly until you are told it was saved.";
    }
}
