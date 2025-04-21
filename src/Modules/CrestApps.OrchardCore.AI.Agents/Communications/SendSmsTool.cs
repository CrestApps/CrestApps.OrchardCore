using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.AI.Agents.Communications;

public sealed class SendSmsTool : AIFunction
{
    public const string TheName = "sendSmsMessage";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISmsService _smsService;
    private readonly IPhoneFormatValidator _phoneFormatValidator;

    public SendSmsTool(
        IHttpContextAccessor httpContextAccessor,
        ISmsService smsService,
        IPhoneFormatValidator phoneFormatValidator)
    {
        _httpContextAccessor = httpContextAccessor;
        _smsService = smsService;
        _phoneFormatValidator = phoneFormatValidator;
        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
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
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Sends an SMS message to a phone number.";

    public override JsonElement JsonSchema { get; }

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!_httpContextAccessor.HttpContext.User.Identity.IsAuthenticated)
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

        if (!_phoneFormatValidator.IsValid(phone))
        {
            return "The given phone number must be in a international format.";
        }

        var message = new SmsMessage()
        {
            To = phone,
            Body = body,
        };

        var result = await _smsService.SendAsync(message);

        if (result.Succeeded)
        {
            return "The SMS message was sent successfully.";
        }

        return $"The SMS message was not sent successfully due to the following: {string.Join(' ', result.Errors)}";
    }
}
