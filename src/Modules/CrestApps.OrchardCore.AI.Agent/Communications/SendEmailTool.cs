using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Email;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.AI.Agent.Communications;

/// <summary>
/// AI tool that performs send email operations.
/// </summary>
public sealed class SendEmailTool : AIFunction
{
    /// <summary>
    /// The name constant.
    /// </summary>
    public const string TheName = "sendEmail";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "to": {
          "type": "string",
          "description": "A valid email address to send to."
        },
        "subject": {
          "type": "string",
          "description": "The email subject to send."
        },
        "body": {
          "type": "string",
          "description": "The email body to send."
        },
        "cc": {
          "type": "string",
          "description": "A comma-delimited emails to carbon-copy."
        },
        "bcc": {
          "type": "string",
          "description": "A comma-delimited emails to blind carbon-copy."
        }
      },
      "additionalProperties": false,
      "required": [
        "to",
        "subject",
        "body"

      ]
    }

    """);
    public override string Name => TheName;

    public override string Description => "Sends an email";

    public override JsonElement JsonSchema => _jsonSchema;

    /// <summary>
    /// Gets the additional properties for the AI function, such as strict mode configuration.
    /// </summary>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<SendEmailTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", Name);
        }

        var emailService = arguments.Services.GetService<IEmailService>();

        if (emailService is null)
        {
            logger.LogWarning("No EmailService is registered. Can't send emails using this tool.");

            return "No EmailService is registered. Can't send emails using this tool.";
        }

        if (!arguments.TryGetFirstString("to", out var to))
        {
            logger.LogWarning("AI tool '{ToolName}' missing required argument '{ArgumentName}'.", Name, "to");

            return "Unable to find a to argument in the function arguments.";
        }

        if (!arguments.TryGetFirstString("subject", out var subject))
        {
            logger.LogWarning("AI tool '{ToolName}' missing required argument '{ArgumentName}'.", Name, "subject");
            return "Unable to find a subject argument in the function arguments.";
        }

        if (!arguments.TryGetFirstString("body", out var body))
        {
            logger.LogWarning("AI tool '{ToolName}' missing required argument '{ArgumentName}'.", Name, "body");
            return "Unable to find a body argument in the function arguments.";
        }

        string senderEmail = null;

        // HttpContext may be null when invoked from a background task (e.g., post-session processing).
        var httpContextAccessor = arguments.Services.GetService<IHttpContextAccessor>();

        var principal = httpContextAccessor?.HttpContext?.User;

        if (principal is not null)
        {
            var userManager = arguments.Services.GetService<UserManager<IUser>>();

            var user = await userManager?.GetUserAsync(principal);

            if (user is not null)
            {
                senderEmail = await userManager.GetEmailAsync(user);
            }
        }

        var message = new MailMessage
        {
            To = to,
            Subject = subject,
            HtmlBody = body,
            Sender = senderEmail,
            From = senderEmail,
            ReplyTo = senderEmail,
        };

        if (arguments.TryGetFirstString("cc", out var cc))
        {
            message.Cc = cc;
        }

        if (arguments.TryGetFirstString("bcc", out var bcc))
        {
            message.Bcc = bcc;
        }

        var result = await emailService.SendAsync(message);

        if (result.Succeeded)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("AI tool '{ToolName}' completed.", Name);
            }
            return "The email was sent successfully.";
        }

        logger.LogWarning("AI tool '{ToolName}' failed to send email to '{To}'.", Name, to);
        return $"The email was not sent successfully due to the following: {string.Join(' ', result.Errors)}";
    }
}
