using System.Text.Json;
using Microsoft.Extensions.AI;

namespace CrestApps.Core.Mvc.Web.Tools;

public sealed class SendEmailTool : AIFunction
{
    public const string TheName = "sendEmail";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "required": [
        "subject",
        "message"
      ],
      "properties": {
        "to": {
          "type": "string",
          "description": "Optional recipient description or email address."
        },
        "subject": {
          "type": "string",
          "description": "The email subject."
        },
        "message": {
          "type": "string",
          "description": "The email body to send."
        }
      },
      "additionalProperties": false
    }
    """);

    public override string Name => TheName;

    public override string Description => "Logs an email request with recipient, subject, and message content.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = true,
    };

    protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!TryGetRequiredString(arguments, "subject", out var subject) ||
            !TryGetRequiredString(arguments, "message", out var message))
        {
            return ValueTask.FromResult<object>("""{"error":"Parameters 'subject' and 'message' are required."}""");
        }

        var logger = arguments.Services.GetRequiredService<ILogger<SendEmailTool>>();

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                    "MVC sendEmail tool invoked. To: {To}; Subject: {Subject}; Message: {Message}",
                    TryGetOptionalString(arguments, "to"),
                    subject,
                    message);
        }

        return ValueTask.FromResult<object>(JsonSerializer.Serialize(new
        {
            success = true,
            subject,
        }));
    }

    private static string TryGetOptionalString(AIFunctionArguments arguments, string key) =>
        arguments.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static bool TryGetRequiredString(AIFunctionArguments arguments, string key, out string value)
    {
        value = TryGetOptionalString(arguments, key);
        return !string.IsNullOrWhiteSpace(value);
    }
}
