using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
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
          "required": ["phone", "body"]
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

        var smsService = arguments.Services.GetRequiredService<ISmsService>();
        var phoneFormatValidator = arguments.Services.GetRequiredService<IPhoneFormatValidator>();

        if (!arguments.IsAuthenticatedOrMcpRequest())
        {
            return "You must login to be able to send SMS message.";
        }

        if (!arguments.TryGetFirstString("phone", out var phone))
        {
            return "Unable to find a phone argument in the function arguments.";
        }

        if (!arguments.TryGetFirstString("body", out var body))
        {
            return "Unable to find a body argument in the function arguments.";
        }

        if (!phoneFormatValidator.IsValid(phone))
        {
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
            return "The SMS message was sent successfully.";
        }

        return $"The SMS message was not sent successfully due to the following: {string.Join(' ', result.Errors)}";
    }
}
