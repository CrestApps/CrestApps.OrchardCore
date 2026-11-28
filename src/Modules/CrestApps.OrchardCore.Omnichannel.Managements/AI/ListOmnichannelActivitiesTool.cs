using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Managements.AI;

/// <summary>
/// Lists CRM activities the way the three admin lists do: the caller's scheduled activities (Interaction Center →
/// Activities), one contact's scheduled or completed activities (the contact's activity page), or the Manage Activities
/// list with its filters. Each scope checks the permission of its page.
/// </summary>
public sealed class ListOmnichannelActivitiesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listOmnichannelActivities";

    private const string _mine = "mine";
    private const string _contact = "contact";
    private const string _all = "all";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "scope": {
          "type": "string",
          "enum": ["mine", "contact", "all"],
          "description": "mine: the caller's scheduled manual activities. contact: one contact's activities (give contactContentItemId). all: the Manage Activities list with its filters. Defaults to mine."
        },
        "contactContentItemId": {
          "type": "string",
          "description": "For scope contact: the content item ID of the contact, lead or other contact record."
        },
        "completed": {
          "type": "boolean",
          "description": "For scope contact: true for the contact's completed activities, false (the default) for the scheduled ones."
        },
        "subjectContentType": {
          "type": "string",
          "description": "For scopes mine and all: only activities about this subject content type."
        },
        "channel": {
          "type": "string",
          "description": "For scopes mine and all: only activities on this channel, such as Phone or SMS."
        },
        "urgencyLevel": {
          "type": "string",
          "enum": ["Normal", "VeryLow", "Low", "Medium", "High", "VeryHigh"],
          "description": "For scopes mine and all: only activities with this urgency."
        },
        "scheduledFrom": {
          "type": "string",
          "description": "For scopes mine and all: only activities scheduled at or after this date and time, in the user's time zone (ISO 8601, such as 2026-11-18T09:00)."
        },
        "scheduledTo": {
          "type": "string",
          "description": "For scopes mine and all: only activities scheduled at or before this date and time, in the user's time zone."
        },
        "status": {
          "type": "string",
          "enum": ["NotStated", "Scheduled", "Pending", "AwaitingAgentResponse", "AwaitingCustomerAnswer", "Reserved", "Dialing", "InProgress", "Completed", "Failed", "Cancelled", "Purged"],
          "description": "For scope all: only activities in this status. NotStated is a pending activity that has not been worked."
        },
        "interactionType": {
          "type": "string",
          "enum": ["Manual", "Automated"],
          "description": "For scope all: only manual or only automated activities."
        },
        "campaignId": {
          "type": "string",
          "description": "For scope all: only activities of this campaign, from listRecords with the kind omnichannelCampaign."
        },
        "dialerProfileId": {
          "type": "string",
          "description": "For scope all: only activities of this dialer profile."
        },
        "assignedToUserIds": {
          "type": "array",
          "items": { "type": "string" },
          "description": "For scope all: only activities assigned to these user IDs."
        },
        "createdFrom": {
          "type": "string",
          "description": "For scope all: only activities created at or after this date and time, in the user's time zone."
        },
        "createdTo": {
          "type": "string",
          "description": "For scope all: only activities created at or before this date and time, in the user's time zone."
        },
        "phoneNumber": {
          "type": "string",
          "description": "For scope all: only activities whose contact has a phone number containing these digits."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, from 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "How many activities a page holds, at most 50. Defaults to 20."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists CRM activities with paging. Scope mine returns the caller's scheduled manual activities, contact returns one contact's scheduled or completed activities, all applies the Manage Activities filters (status, campaign, dialer profile, assignee, dates, phone number). Use it to find activity ids before any change. Dates are in the user's time zone.";

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
        var caller = AIModuleTools.GetCaller(services);

        if (caller is null)
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var scope = (AIModuleTools.ReadString(arguments, "scope") ?? _mine).ToLowerInvariant();

        if (scope is not (_mine or _contact or _all))
        {
            return "Nothing was listed: scope must be mine, contact or all.";
        }

        if (!OmnichannelToolSupport.TryReadEnum<ActivityUrgencyLevel>(arguments, "urgencyLevel", out var urgencyLevel))
        {
            return $"Nothing was listed: urgencyLevel must be one of {OmnichannelToolSupport.Names<ActivityUrgencyLevel>()}.";
        }

        var (validFrom, scheduledFrom) = await OmnichannelToolSupport.TryReadLocalDateTimeAsync(services, arguments, "scheduledFrom");
        var (validTo, scheduledTo) = await OmnichannelToolSupport.TryReadLocalDateTimeAsync(services, arguments, "scheduledTo");

        if (!validFrom || !validTo)
        {
            return "Nothing was listed: scheduledFrom and scheduledTo must be dates and times such as 2026-11-18T09:00.";
        }

        var manager = services.GetRequiredService<IOmnichannelActivityManager>();
        var authorizationService = services.GetRequiredService<IAuthorizationService>();
        var (page, pageSize) = AIModuleTools.ReadPaging(arguments);
        PageResult<OmnichannelActivity> result;

        switch (scope)
        {
            case _contact:
                var contactId = AIModuleTools.ReadString(arguments, "contactContentItemId");

                if (contactId is null)
                {
                    return "Nothing was listed: scope contact needs contactContentItemId.";
                }

                var contact = await services.GetRequiredService<IContentManager>().GetAsync(contactId, VersionOptions.Published);

                if (contact is null)
                {
                    // Said the same way whether the caller may see the contact or not.
                    return await authorizationService.AuthorizeAsync(caller, OmnichannelConstants.Permissions.ListContactActivities)
                        ? $"Nothing was listed: no published contact has the ID '{contactId}'."
                        : AIToolCallerAuthorization.NotAllowedMessage;
                }

                if (!await authorizationService.AuthorizeAsync(caller, OmnichannelConstants.Permissions.ListContactActivities, contact))
                {
                    return AIToolCallerAuthorization.NotAllowedMessage;
                }

                var completed = arguments.TryGetValue("completed", out var completedValue) && IsTrue(completedValue);

                result = completed
                    ? await manager.PageContactManualCompletedAsync(contactId, page, pageSize, cancellationToken)
                    : await manager.PageContactManualScheduledAsync(contactId, page, pageSize, cancellationToken);
                break;

            case _all:
                if (!await authorizationService.AuthorizeAsync(caller, OmnichannelConstants.Permissions.ManageActivities))
                {
                    return AIToolCallerAuthorization.NotAllowedMessage;
                }

                var filter = await BuildBulkFilterAsync(services, arguments, urgencyLevel, scheduledFrom, scheduledTo);

                if (filter is null)
                {
                    return "Nothing was listed: status, interactionType, createdFrom, createdTo or phoneNumber is not valid. Use the values the parameters list, dates such as 2026-11-18T09:00, and a phone number with at least one digit.";
                }

                result = await manager.PageBulkManageableAsync(page, pageSize, filter, cancellationToken);
                break;

            default:
                if (!await authorizationService.AuthorizeAsync(caller, OmnichannelConstants.Permissions.ListActivities))
                {
                    return AIToolCallerAuthorization.NotAllowedMessage;
                }

                result = await manager.PageManualScheduledAsync(caller.FindFirstValue(ClaimTypes.NameIdentifier), page, pageSize, new ListOmnichannelActivityFilter
                {
                    SubjectContentType = AIModuleTools.ReadString(arguments, "subjectContentType"),
                    Channel = AIModuleTools.ReadString(arguments, "channel"),
                    UrgencyLevel = urgencyLevel,
                    ScheduledFrom = scheduledFrom,
                    ScheduledTo = scheduledTo,
                }, cancellationToken);
                break;
        }

        var contacts = await ActivityToolSupport.GetContactsAsync(services, result.Entries);
        var items = new List<object>();

        foreach (var activity in result.Entries)
        {
            items.Add(await ActivityToolSupport.DescribeAsync(services, activity, contacts));
        }

        return AIModuleTools.Json(new
        {
            scope,
            items,
            total = result.Count,
            page,
            pageSize,
            truncated = page * pageSize < result.Count,
        });
    }

    private static async Task<BulkManageActivityFilter> BuildBulkFilterAsync(
        IServiceProvider services,
        AIFunctionArguments arguments,
        ActivityUrgencyLevel? urgencyLevel,
        DateTime? scheduledFrom,
        DateTime? scheduledTo)
    {
        if (!OmnichannelToolSupport.TryReadEnum<ActivityStatus>(arguments, "status", out var status) ||
            !OmnichannelToolSupport.TryReadEnum<ActivityInteractionType>(arguments, "interactionType", out var interactionType))
        {
            return null;
        }

        var (validCreatedFrom, createdFrom) = await OmnichannelToolSupport.TryReadLocalDateTimeAsync(services, arguments, "createdFrom");
        var (validCreatedTo, createdTo) = await OmnichannelToolSupport.TryReadLocalDateTimeAsync(services, arguments, "createdTo");
        var phoneNumber = AIModuleTools.ReadString(arguments, "phoneNumber");

        // The Manage Activities filter refuses a phone number without a digit.
        if (!validCreatedFrom || !validCreatedTo || (phoneNumber is not null && !PhoneNumberSearchTerm.TryParse(phoneNumber, out _)))
        {
            return null;
        }

        var assignedTo = OmnichannelToolSupport.ReadStrings(arguments, "assignedToUserIds");

        return new BulkManageActivityFilter
        {
            SubjectContentType = AIModuleTools.ReadString(arguments, "subjectContentType"),
            Channel = AIModuleTools.ReadString(arguments, "channel"),
            CampaignId = AIModuleTools.ReadString(arguments, "campaignId"),
            DialerProfileId = AIModuleTools.ReadString(arguments, "dialerProfileId"),
            AssignedToUserIds = assignedTo is { Length: > 0 } ? assignedTo : null,
            Status = status,
            InteractionType = interactionType,
            UrgencyLevel = urgencyLevel,
            ScheduledFrom = scheduledFrom,
            ScheduledTo = scheduledTo,
            CreatedFrom = createdFrom,
            CreatedTo = createdTo,
            PhoneNumber = phoneNumber,
        };
    }

    private static bool IsTrue(object value)
        => value switch
        {
            bool flag => flag,
            JsonElement { ValueKind: JsonValueKind.True } => true,
            JsonElement { ValueKind: JsonValueKind.String } element => bool.TryParse(element.GetString(), out var parsed) && parsed,
            string text => bool.TryParse(text, out var parsed) && parsed,
            _ => false,
        };
}
