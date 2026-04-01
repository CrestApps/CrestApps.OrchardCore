using System.Text.Json;
using CrestApps.AI.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.AI.Agent.Communications;

public sealed class SendSmsTool : AIFunction
{
    public const string TheName = "sendSmsMessage";
    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "phone": {
          "type": "string",
          "description": "This must be internationally formatted phone number starting with +."
        },
        "body": {
          "type": "string",
          "description": "The text message body to send."
        }
      },
      "additionalProperties": false,
      "required": [
        "phone",
        "body"
      ]
    }
    """);
    public override string Name => TheName;
    public override string Description => "Sends an SMS message to a phone number.";
    public override JsonElement JsonSchema => _jsonSchema;
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<SendSmsTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", Name);
        }

        var smsService = arguments.Services.GetRequiredService<ISmsService>();
        var phoneFormatValidator = arguments.Services.GetRequiredService<IPhoneFormatValidator>();

        if (!arguments.TryGetFirstString("phone", out var phone))
        {
            logger.LogWarning("AI tool '{ToolName}' missing required argument '{ArgumentName}'.", Name, "phone");

            return "Unable to find a phone argument in the function arguments.";
        }

        if (!arguments.TryGetFirstString("body", out var body))
        {
            logger.LogWarning("AI tool '{ToolName}' missing required argument '{ArgumentName}'.", Name, "body");

            return "Unable to find a body argument in the function arguments.";
        }

        if (!phoneFormatValidator.IsValid(phone))
        {
            logger.LogWarning("AI tool '{ToolName}' received invalid phone format '{Phone}'.", Name, phone);

            return "The given phone number must be in a international format.";
        }

        var message = new SmsMessage()
        {
            To = phone,
            Body = body,
        };

        var result = await smsService.SendAsync(message);

        if (result.Succeeded)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("AI tool '{ToolName}' completed.", Name);
            }

            return "The SMS message was sent successfully.";
        }

        logger.LogWarning("AI tool '{ToolName}' failed to send SMS to '{Phone}'.", Name, phone);

        return $"The SMS message was not sent successfully due to the following: {string.Join(' ', result.Errors)}";
    }
}
