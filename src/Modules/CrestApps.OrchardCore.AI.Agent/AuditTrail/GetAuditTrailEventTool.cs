using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.AuditTrail;
using OrchardCore.AuditTrail.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.AuditTrail;

/// <summary>
/// Gets one audit trail event through the audit trail manager, as the Display page of the Audit Trail admin area
/// does. That page shows the data of an event through the display driver of the module that recorded it; the tool
/// has no display drivers, so it knows the two event types of Orchard Core, user events and content events, by the
/// name their data is stored under, and returns the header only for any other type. The details of a user event need
/// the permission the Users module checks: the one for the caller's own events when the event is about the caller,
/// the one for the events about any user otherwise. Every detail is built from an explicit list of fields; the stored
/// user snapshot and the stored content item are never returned whole.
/// </summary>
public sealed class GetAuditTrailEventTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getAuditTrailEvent";

    // The names the Users and Contents modules store the data of their events under: the names of their event models,
    // which live in module assemblies this module cannot reference.
    private const string UserEventData = "AuditTrailUserEvent";
    private const string ContentEventData = "AuditTrailContentEvent";

    // The fields of a user snapshot a tool may return. A snapshot holds whatever properties of the user the site is
    // set to record, so the security stamp, the sign-in tokens, the passkeys and the external logins stay out by not
    // being on this list.
    private static readonly string[] _snapshotFields =
    [
        "UserId",
        "UserName",
        "Email",
        "EmailConfirmed",
        "PhoneNumber",
        "PhoneNumberConfirmed",
        "IsEnabled",
        "RoleNames",
        "TwoFactorEnabled",
        "IsLockoutEnabled",
        "AccessFailedCount",
    ];

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "eventId": {
          "type": "string",
          "description": "The id of the event, from listAuditTrailEvents."
        }
      },
      "required": [
        "eventId"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Gets one audit trail event by id with its details, such as the user snapshot or the content item version it recorded, and the IP address when the site records it. The details of an event about a user need the permission to view user audit trail events.";

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
        var caller = AgentToolAuthorization.GetCaller(services);

        if (caller is null || !await AgentToolAuthorization.IsAuthorizedAsync(services, AuditTrailPermissions.ViewAuditTrail))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("eventId", out var eventId))
        {
            return "Nothing was found: give the eventId of the event. Use listAuditTrailEvents to find it.";
        }

        var auditTrailEvent = await services.GetRequiredService<IAuditTrailManager>().GetEventAsync(eventId.Trim());

        if (auditTrailEvent is null)
        {
            return $"Nothing was found: there is no audit trail event with the id '{eventId}'. Use listAuditTrailEvents to find it.";
        }

        object user = null;
        object content = null;
        string detailsNote = null;

        if (auditTrailEvent.Properties?[UserEventData] is JsonObject userEvent)
        {
            if (await CanSeeUserEventAsync(services, caller, userEvent))
            {
                user = DescribeUserEvent(userEvent);
            }
            else
            {
                detailsNote = "The details of this event are about a user, and you are not allowed to view audit trail events about users.";
            }
        }
        else if (auditTrailEvent.Properties?[ContentEventData] is JsonObject contentEvent)
        {
            content = await DescribeContentEventAsync(services, contentEvent);
        }
        else
        {
            detailsNote = "This type of event has details this tool does not know. A person sees them on the admin page of the event.";
        }

        return AgentToolResults.Json(new
        {
            auditTrailEvent = await AuditTrailToolSupport.SummarizeAsync(services, auditTrailEvent),
            clientIpAddress = string.IsNullOrEmpty(auditTrailEvent.ClientIpAddress)
                ? null
                : auditTrailEvent.ClientIpAddress,
            user,
            content,
            detailsNote,
            adminUrl = AuditTrailToolSupport.GetAdminUrl(services, auditTrailEvent.EventId),
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(services.GetRequiredService<ILocalClock>()),
        });
    }

    private static async Task<bool> CanSeeUserEventAsync(IServiceProvider services, ClaimsPrincipal caller, JsonObject userEvent)
    {
        // What the authorization handler of the Users module decides for the details of a user event: the caller's own
        // events need the permission for own events, the events about anyone else the permission for every user.
        var callerId = caller.FindFirstValue(ClaimTypes.NameIdentifier);
        var isOwn = !string.IsNullOrEmpty(callerId) &&
            string.Equals(callerId, ReadText(userEvent["UserId"]), StringComparison.OrdinalIgnoreCase);

        return await AgentToolAuthorization.IsAuthorizedAsync(services, isOwn
            ? OrchardCorePermissions.ViewOwnUserAuditTrailEvents
            : OrchardCorePermissions.ViewUserAuditTrailEvents);
    }

    private static object DescribeUserEvent(JsonObject userEvent)
    {
        Dictionary<string, object> snapshot = null;

        if (userEvent["Snapshot"] is JsonObject stored)
        {
            snapshot = [];

            foreach (var field in _snapshotFields)
            {
                // The values were redacted as the site is set to when the event was recorded, and are returned as stored.
                switch (stored[field])
                {
                    case JsonValue value:
                        snapshot[field] = value.ToString();
                        break;
                    case JsonArray values:
                        snapshot[field] = values.OfType<JsonValue>().Select(value => value.ToString()).ToList();
                        break;
                }
            }
        }

        return new
        {
            userId = ReadText(userEvent["UserId"]),
            userName = ReadText(userEvent["UserName"]),
            snapshot,
        };
    }

    private static async Task<object> DescribeContentEventAsync(IServiceProvider services, JsonObject contentEvent)
    {
        var contentItem = contentEvent["ContentItem"] as JsonObject;
        DateTime? modifiedUtc = null;

        if (contentItem?["ModifiedUtc"] is JsonValue modified && modified.TryGetValue<DateTime>(out var modifiedValue))
        {
            modifiedUtc = modifiedValue;
        }

        return new
        {
            contentItemId = ReadText(contentItem?["ContentItemId"]),
            contentItemVersionId = ReadText(contentItem?["ContentItemVersionId"]),
            contentType = ReadText(contentItem?["ContentType"]),
            displayText = ReadText(contentItem?["DisplayText"]),
            versionNumber = contentEvent["VersionNumber"] is JsonValue version && version.TryGetValue<int>(out var versionNumber)
                ? versionNumber
                : (int?)null,
            published = ReadFlag(contentItem?["Published"]),
            latest = ReadFlag(contentItem?["Latest"]),
            author = ReadText(contentItem?["Author"]),
            modifiedAt = await AgentToolResults.ToLocalAsync(services.GetRequiredService<ILocalClock>(), modifiedUtc),
            comment = ReadText(contentEvent["Comment"]),
            note = "The event recorded this version of the content item. Use the content tools with the content item id to read the item or its versions.",
        };
    }

    private static string ReadText(JsonNode node)
    {
        return node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrEmpty(text)
            ? text
            : null;
    }

    private static bool? ReadFlag(JsonNode node)
    {
        return node is JsonValue value && value.TryGetValue<bool>(out var flag)
            ? flag
            : null;
    }
}
