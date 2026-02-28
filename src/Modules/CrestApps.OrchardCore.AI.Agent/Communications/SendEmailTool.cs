using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Email;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.AI.Agent.Communications;

public sealed class SendEmailTool : AIFunction
{
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
          "required": ["to", "subject", "body"]
        }
        """);

    public override string Name => TheName;

    public override string Description => "Sends an email";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var httpContextAccessor = arguments.Services.GetRequiredService<IHttpContextAccessor>();
        var userManager = arguments.Services.GetRequiredService<UserManager<IUser>>();
        var emailService = arguments.Services.GetRequiredService<IEmailService>();

        if (!arguments.TryGetFirstString("to", out var to))
        {
            return "Unable to find a to argument in the function arguments.";
        }

        if (!arguments.TryGetFirstString("subject", out var subject))
        {
            return "Unable to find a subject argument in the function arguments.";
        }

        if (!arguments.TryGetFirstString("body", out var body))
        {
            return "Unable to find a body argument in the function arguments.";
        }

        if (!arguments.IsAuthenticatedOrMcpRequest())
        {
            return "You must login to be able to send email.";
        }

        var user = await userManager.GetUserAsync(httpContextAccessor.HttpContext.User);

        if (user is null)
        {
            return "You must login to be able to send email.";
        }

        var email = await userManager.GetEmailAsync(user);

        if (string.IsNullOrEmpty(email))
        {
            return "You do no have an email on associated with the user.";
        }

        var message = new MailMessage
        {
            To = to,
            Subject = subject,
            HtmlBody = body,
            Sender = email,
            From = email,
            ReplyTo = email,
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
            return "The email was sent successfully.";
        }

        return $"The email was not sent successfully due to the following: {string.Join(' ', result.Errors)}";
    }
}
