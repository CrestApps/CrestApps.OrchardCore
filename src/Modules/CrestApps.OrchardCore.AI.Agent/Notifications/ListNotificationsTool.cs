using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Modules;
using OrchardCore.Navigation.Core;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;

namespace CrestApps.OrchardCore.AI.Agent.Notifications;

/// <summary>
/// Lists the notifications of the signed-in caller with the filter parser and the query service of the Notifications
/// admin page: the arguments become the filter text a person types on that page. That query always keeps to the
/// notifications of the user of the request, so the tool cannot list anybody else's.
/// </summary>
public sealed class ListNotificationsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listNotifications";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "status": {
          "type": "string",
          "enum": [
            "read",
            "unread"
          ],
          "description": "List the read notifications only, or the unread ones only. Leave it out for both."
        },
        "type": {
          "type": "string",
          "description": "The technical name of a type of notification, to list the notifications of that type only. The result names the types the site knows."
        },
        "text": {
          "type": "string",
          "description": "Text the notification must contain."
        },
        "sort": {
          "type": "string",
          "enum": [
            "newest",
            "oldest"
          ],
          "description": "The order of the notifications. The default is newest first."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1. The default is 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of notifications on a page. The default is 20 and the most is 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the calling user's own notifications, newest first, with optional filters for read or unread, type and text, and paging. It cannot list the notifications of another user.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;
        var callerId = NotificationToolSupport.GetCallerId(services);

        if (callerId is null || !await AgentToolAuthorization.IsAuthorizedAsync(services, NotificationPermissions.ManageNotifications))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var terms = new List<string>();

        if (arguments.TryGetFirstString("status", out var status))
        {
            switch (status.Trim().ToLowerInvariant())
            {
                case "read":
                    terms.Add($"status:{nameof(NotificationStatus.Read)}");
                    break;
                case "unread":
                    terms.Add($"status:{nameof(NotificationStatus.Unread)}");
                    break;
                default:
                    return "Nothing was listed: status must be read or unread.";
            }
        }

        if (arguments.TryGetFirstString("type", out var type))
        {
            type = type.Trim();

            if (type.Length > NotificationConstants.NotificationTypeLength || !type.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.'))
            {
                return "Nothing was listed: type must be the technical name of a type of notification, without spaces or quotes.";
            }

            terms.Add($"type:{type}");
        }

        if (arguments.TryGetFirstString("sort", out var sort))
        {
            switch (sort.Trim().ToLowerInvariant())
            {
                case "newest":
                    break;
                case "oldest":
                    terms.Add("sort:Oldest");
                    break;
                default:
                    return "Nothing was listed: sort must be newest or oldest.";
            }
        }

        // The text is the part of the filter without a name. It goes last and in quotes, after the named filters.
        if (arguments.TryGetFirstString("text", out var text) && !string.IsNullOrWhiteSpace(text))
        {
            if (text.Contains('"'))
            {
                return "Nothing was listed: text cannot contain a double quote.";
            }

            terms.Add($"\"{text.Trim()}\"");
        }

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        var options = new ListNotificationOptions
        {
            FilterResult = services.GetRequiredService<INotificationAdminListFilterParser>().Parse(string.Join(' ', terms)),
        };

        var result = await services.GetRequiredService<INotificationsAdminListQueryService>().QueryAsync(page, pageSize, options, new NullModelUpdater());
        var items = new List<object>();

        // The query keeps to the user of the request. A notification of anybody else is left out all the same.
        foreach (var notification in result.Notifications.Where(notification => string.Equals(notification.UserId, callerId, StringComparison.Ordinal)))
        {
            items.Add(await NotificationToolSupport.DescribeAsync(services, notification));
        }

        return AgentToolResults.Json(new
        {
            items,
            total = result.TotalCount,
            page,
            pageSize,
            truncated = result.TotalCount > page * pageSize
                ? true
                : (bool?)null,
            knownTypes = services.GetService<IOptions<NotificationTypeOptions>>()?.Value.Types.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList(),
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(services.GetRequiredService<ILocalClock>()),
            note = "These are your own notifications. Use markNotifications to mark them as read or unread.",
        });
    }
}
