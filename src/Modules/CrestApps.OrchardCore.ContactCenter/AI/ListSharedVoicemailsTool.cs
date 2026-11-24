using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Controllers;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Lists the messages left in the shared voicemail boxes of the queues the caller may see, as the Shared voicemail page
/// lists them. <see cref="ISharedVoicemailService"/> authorizes the read against the caller's queue entitlements.
/// </summary>
internal sealed class ListSharedVoicemailsTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listSharedVoicemails";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "queue": {
          "type": "string",
          "description": "Only voicemails of this queue, by id or name. Every queue the caller may see when not given."
        },
        "status": {
          "type": "string",
          "enum": ["Open", "New", "Claimed", "Resolved", "All"],
          "description": "Which voicemails by their handling: Open (new or claimed, the default), New, Claimed, Resolved or All."
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
          "description": "Voicemails per page, 20 when not given, at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists shared queue voicemails by queue and status with caller, time, duration and who claimed them, and what the caller may do with each. Only the queues the caller is entitled to are included. Caller numbers are shown by their last four digits.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ContactCenterPermissions.AccessSharedVoicemail;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var access = await services.GetRequiredService<ISharedVoicemailAuthorizationService>().GetAccessAsync(user, cancellationToken);

        if (!access.CanAccess)
        {
            return NotAllowedMessage;
        }

        var reader = new ContactCenterToolArguments(arguments);
        var (page, pageSize) = ReadPaging(reader);

        if (!reader.TryReadChoice<SharedVoicemailStatusFilter>("status", null, out var status))
        {
            return string.Join(" ", reader.Errors);
        }

        var queues = (await services.GetRequiredService<IActivityQueueManager>().GetAllAsync(cancellationToken))
            .Where(queue => queue is not null && access.CoversQueue(queue.ItemId))
            .ToList();
        var queueNames = queues.ToDictionary(queue => queue.ItemId, queue => queue.Name, StringComparer.OrdinalIgnoreCase);
        var queueText = reader.GetString("queue");
        string queueId = null;

        if (queueText is not null)
        {
            var matches = queues
                .Where(queue => string.Equals(queue.ItemId, queueText, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(queue.Name, queueText, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count != 1)
            {
                return matches.Count == 0
                    ? $"No queue you may see voicemail of has the id or name '{queueText}'."
                    : $"More than one queue is named '{queueText}'. Give its id.";
            }

            queueId = matches[0].ItemId;
        }

        var query = new SharedVoicemailQuery
        {
            QueueIds = queueId is null ? null : [queueId],
            Page = page,
            PageSize = pageSize,
        };
        SharedVoicemailController.ApplyStatus(query, status ?? SharedVoicemailStatusFilter.Open);

        var result = await services.GetRequiredService<ISharedVoicemailService>().ListAsync(user, query, cancellationToken);
        var interactions = services.GetRequiredService<IInteractionManager>();
        var localClock = services.GetRequiredService<ILocalClock>();
        var items = new List<object>();

        foreach (var voicemail in result.Entries)
        {
            var interaction = string.IsNullOrEmpty(voicemail.InteractionId)
                ? null
                : await interactions.FindByIdAsync(voicemail.InteractionId, cancellationToken);

            items.Add(new
            {
                id = voicemail.ItemId,
                queue = ContactCenterSetupLookups.Reference(voicemail.QueueId, queueNames),
                status = voicemail.Status.ToString(),
                callerName = voicemail.CallerName,
                callerNumberEnding = ContactCenterOperationsSupport.Ending(voicemail.CallerNumber),
                contactId = voicemail.ContactContentItemId,
                received = await ToLocalAsync(localClock, voicemail.ReceivedUtc),
                durationSeconds = SharedVoicemailRecordingInfo.GetDurationSeconds(interaction, voicemail),
                hasRecording = SharedVoicemailRecordingInfo.HasRecording(interaction),
                claimedBy = voicemail.Status == SharedVoicemailStatus.Claimed ? voicemail.ClaimedByUserName : null,
                claimed = voicemail.Status == SharedVoicemailStatus.Claimed ? await ToLocalAsync(localClock, voicemail.ClaimedUtc) : null,
                claimedByYou = access.Holds(voicemail),
                resolvedBy = voicemail.Status == SharedVoicemailStatus.Resolved ? voicemail.ResolvedByUserName : null,
                resolved = voicemail.Status == SharedVoicemailStatus.Resolved ? await ToLocalAsync(localClock, voicemail.ResolvedUtc) : null,
                resolutionNote = voicemail.Status == SharedVoicemailStatus.Resolved ? voicemail.ResolutionNote : null,
                callBackStartedBy = voicemail.CallbackRequestedByUserName,
                youCan = new
                {
                    claim = access.CanClaim(voicemail),
                    release = access.CanRelease(voicemail),
                    resolve = access.CanWork(voicemail),
                    callBack = access.CanWork(voicemail) && !string.IsNullOrWhiteSpace(voicemail.CallerNumber),
                    delete = access.CanDelete(voicemail),
                },
            });
        }

        return Json(new
        {
            items,
            total = result.Count,
            page,
            pageSize,
            truncated = result.Count > page * pageSize,
        });
    }

    /// <summary>
    /// Says why the shared voicemail service refused an action, in the words of the Shared voicemail page.
    /// </summary>
    internal static string DescribeFailure(SharedVoicemailActionResult result)
        => "Nothing was done: " + result.ReasonCode switch
        {
            SharedVoicemailReasons.ClaimedByAnotherUser => $"{result.Voicemail?.ClaimedByUserName ?? "someone else"} is already handling this voicemail.",
            SharedVoicemailReasons.AlreadyResolved => "this voicemail was already marked as done. Release it back to the queue to work it again.",
            SharedVoicemailReasons.ManagePermissionRequired => "you are not allowed to delete shared voicemail or take over another person's voicemail.",
            SharedVoicemailReasons.NoCallerNumber => "the caller left no number to call back.",
            SharedVoicemailReasons.CallbacksUnavailable => "calling back is not available. The call is placed from your own soft phone, so a telephony provider must be configured and your soft phone must be signed in.",
            SharedVoicemailReasons.CallbackAlreadyStarted => "you are already calling this caller back. If your soft phone did not start the call, wait a moment and try again.",
            SharedVoicemailReasons.LegalHold => "the voicemail's recording is under legal hold and cannot be deleted.",
            _ when result.Status == SharedVoicemailActionStatus.NotFound => "the voicemail could not be found. Use listSharedVoicemails to find it.",
            _ => "you are not allowed to do that with this voicemail.",
        };
}
