using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.AI.Agent.Communications;

public sealed class SendNotificationTool : AIFunction
{
    public const string TheName = "sendNotification";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<IUser> _userManager;
    private readonly INotificationService _notificationService;

    public SendNotificationTool(
        IHttpContextAccessor httpContextAccessor,
        UserManager<IUser> userManager,
        INotificationService notificationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
        _notificationService = notificationService;
        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
           """
            {
              "type": "object",
              "properties": {
                "userId": {
                  "type": "string",
                  "description": "The unique identifier of the user to notify. This should be a valid user ID from your system."
                },
                "subject": {
                  "type": "string",
                  "description": "The subject line of the notification. This appears as the title or headline of the message."
                },
                "summary": {
                  "type": "string",
                  "description": "A short summary of the notification content. May include limited HTML for styling in the UI. Keep it concise and informative."
                },
                "textBody": {
                  "type": "string",
                  "description": "The plain text version of the notification body. Should contain no HTML and be suitable for text-only clients."
                },
                "htmlBody": {
                  "type": "string",
                  "description": "The HTML version of the notification body. This can include formatting, links, and other HTML content for rich display."
                }
              },
              "required": ["userId", "subject", "summary"],
              "additionalProperties": false
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Sends an notification to a user.";

    public override JsonElement JsonSchema { get; }

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!_httpContextAccessor.HttpContext.User.Identity.IsAuthenticated)
        {
            return "You must login to be able to send notification.";
        }

        if (!arguments.TryGetFirstString("userId", out var userId))
        {
            return "Unable to find a userId argument in the function arguments.";
        }

        if (!arguments.TryGetFirstString("subject", out var subject))
        {
            return "Unable to find a subject argument in the function arguments.";
        }

        if (!arguments.TryGetFirstString("summary", out var summary))
        {
            return "Unable to find a summary argument in the function arguments.";
        }

        var textBody = arguments.GetFirstValueOrDefault("textBody", summary);

        var message = new NotificationMessage
        {
            Subject = subject,
            Summary = summary,
            TextBody = textBody,
            HtmlBody = arguments.GetFirstValueOrDefault<string>("htmlBody"),
        };

        message.IsHtmlPreferred = !string.IsNullOrEmpty(message.HtmlBody);

        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return "Unable to find a user that matches the given userId: " + userId;
        }

        var count = await _notificationService.SendAsync(user, message);

        if (count > 0)
        {
            return "The user was notified successfully.";
        }

        return "The notification was not sent successfully.";
    }
}
