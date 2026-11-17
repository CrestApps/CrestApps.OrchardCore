using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Settings;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents;
using OrchardCore.Modules;
using YSession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Agent.PublishLater;

/// <summary>
/// Schedules a content item to be published or archived, or cancels the schedule. It sets the date the Publish Later
/// and Archive Later editors set, converted from local time the same way, and saves the item the way those editors'
/// buttons do: a scheduled publish is saved as a draft, a scheduled archive is published.
/// </summary>
public sealed class ScheduleContentItemTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "scheduleContentItem";

    private const string PublishAction = "publish";

    private const string ArchiveAction = "archive";

    private static readonly string[] _localFormats =
    [
        "yyyy-MM-dd'T'HH:mm",
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd HH:mm:ss",
    ];

    private static readonly JsonMergeSettings _mergeNulls = new()
    {
        MergeNullValueHandling = MergeNullValueHandling.Merge,
    };

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The ID of the content item (ContentItemId)."
        },
        "action": {
          "type": "string",
          "enum": [
            "publish",
            "archive"
          ],
          "description": "What happens at the scheduled time: publish (the draft is published) or archive (the published item is unpublished)."
        },
        "when": {
          "type": "string",
          "description": "The local date and time, in the site's or the user's time zone, as yyyy-MM-ddTHH:mm, for example 2026-12-24T08:00. Do not add a time zone or Z. Leave it out when cancel is true."
        },
        "cancel": {
          "type": "boolean",
          "description": "True to cancel the schedule of the given action instead of setting one."
        }
      },
      "required": [
        "contentItemId",
        "action"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Schedules a content item to be published or archived (unpublished) at a date and time given in the site's time zone, or cancels the schedule. The content type must have the Publish Later or Archive Later part; publishing is scheduled on a draft, archiving on a published item.";

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

        if (AgentToolAuthorization.GetCaller(services) is null)
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("contentItemId", out var contentItemId))
        {
            return "The contentItemId argument is required.";
        }

        if (!arguments.TryGetFirstString("action", out var action) ||
            (!string.Equals(action, PublishAction, StringComparison.OrdinalIgnoreCase) && !string.Equals(action, ArchiveAction, StringComparison.OrdinalIgnoreCase)))
        {
            return "Nothing was scheduled: action must be publish or archive.";
        }

        var publishes = string.Equals(action, PublishAction, StringComparison.OrdinalIgnoreCase);
        var cancel = arguments.GetFirstValueOrDefault("cancel", false);
        var hasWhen = arguments.TryGetFirstString("when", out var when);

        if (cancel == hasWhen)
        {
            return "Nothing was scheduled: give when to set the schedule, or cancel as true to remove it, not both.";
        }

        var contentManager = services.GetRequiredService<IContentManager>();
        var contentItem = await contentManager.GetAsync(contentItemId, VersionOptions.Latest);

        if (contentItem is null)
        {
            return $"No content item with the ID '{contentItemId}' was found.";
        }

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, CommonPermissions.PublishContent, contentItem))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        // The parts are in the Publish Later and Archive Later modules, so the stored JSON is written by name.
        var partName = publishes ? "PublishLaterPart" : "ArchiveLaterPart";
        var propertyName = publishes ? "ScheduledPublishUtc" : "ScheduledArchiveUtc";
        var featureName = publishes ? "Publish Later" : "Archive Later";
        var typeDefinition = await services.GetRequiredService<IContentDefinitionManager>().GetTypeDefinitionAsync(contentItem.ContentType);

        if (typeDefinition is null || !typeDefinition.Parts.Any(part => part.PartDefinition?.Name == partName))
        {
            return $"Nothing was scheduled: the content type '{contentItem.ContentType}' does not have the {featureName} part. Add the part '{partName}' to the content type first.";
        }

        if (publishes && contentItem.Published)
        {
            return cancel
                ? $"Nothing was changed: content item '{contentItemId}' has no draft, so no publishing is scheduled."
                : $"Nothing was scheduled: the latest version of content item '{contentItemId}' is published. Publishing is scheduled on a draft; save a draft of the item first.";
        }

        if (!publishes && !contentItem.Published)
        {
            return cancel
                ? $"Nothing was changed: the latest version of content item '{contentItemId}' is a draft, and archiving is scheduled on the published version. Publish or discard the draft first."
                : $"Nothing was scheduled: the latest version of content item '{contentItemId}' is not published. Archiving is scheduled on a published item; publish the item, or publish or discard its draft, first.";
        }

        var localClock = services.GetRequiredService<ILocalClock>();
        var current = ((JsonObject)contentItem.Content)[partName]?[propertyName];
        DateTime? scheduledUtc = null;

        if (cancel)
        {
            if (current is null)
            {
                return $"Nothing was changed: content item '{contentItemId}' has no scheduled {(publishes ? "publishing" : "archiving")}.";
            }
        }
        else
        {
            if (!DateTime.TryParseExact(when.Trim(), _localFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            {
                return "Nothing was scheduled: when must be a local date and time as yyyy-MM-ddTHH:mm, for example 2026-12-24T08:00, without a time zone.";
            }

            scheduledUtc = DateTime.SpecifyKind(await localClock.ConvertToUtcAsync(local), DateTimeKind.Utc);

            if (scheduledUtc <= services.GetRequiredService<IClock>().UtcNow)
            {
                return $"Nothing was scheduled: {when} is in the past in the time zone '{await AgentToolResults.GetTimeZoneIdAsync(localClock)}'.";
            }
        }

        // As the editor does: the item is edited as a draft, then saved as a draft (publish later) or published (archive later).
        var draft = await contentManager.GetAsync(contentItemId, VersionOptions.DraftRequired);

        draft.Merge(new JsonObject
        {
            [partName] = new JsonObject
            {
                [propertyName] = scheduledUtc,
            },
        }, _mergeNulls);

        await contentManager.UpdateAsync(draft);

        var result = await contentManager.ValidateAsync(draft);

        if (!result.Succeeded)
        {
            await services.GetRequiredService<YSession>().CancelAsync();

            return $"Nothing was scheduled: the content item is not valid. {string.Join(" ", result.Errors.Select(error => error.ErrorMessage))}".TrimEnd();
        }

        if (publishes)
        {
            await contentManager.SaveDraftAsync(draft);
        }
        else
        {
            await contentManager.PublishAsync(draft);
        }

        return AgentToolResults.Json(new
        {
            scheduled = !cancel,
            cancelled = cancel ? true : (bool?)null,
            action = publishes ? PublishAction : ArchiveAction,
            contentItemId = draft.ContentItemId,
            displayText = draft.DisplayText,
            scheduledFor = await AgentToolResults.ToLocalAsync(localClock, scheduledUtc),
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
            note = cancel
                ? null
                : $"The {featureName} background task does the {(publishes ? "publishing" : "archiving")} when the time comes. It runs every minute and must be enabled.",
        });
    }
}
