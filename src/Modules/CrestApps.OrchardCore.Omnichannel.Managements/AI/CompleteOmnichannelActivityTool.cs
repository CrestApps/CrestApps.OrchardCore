using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Omnichannel.Managements.AI;

/// <summary>
/// Completes an activity with a disposition and notes, as the Complete page does: the disposition must be one the
/// activity's subject offers, and the activity disposition service then runs the subject's actions.
/// </summary>
public sealed class CompleteOmnichannelActivityTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "completeOmnichannelActivity";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the activity to complete, from listOmnichannelActivities."
        },
        "dispositionId": {
          "type": "string",
          "description": "The ID of the disposition, from listRecords with the kind omnichannelDisposition. It must be one the activity's subject is configured for. Required when the subject flow requires a disposition."
        },
        "notes": {
          "type": "string",
          "description": "Notes about the outcome. They are added after any notes the activity already has."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Completes an activity with a disposition and notes. The disposition's subject actions then run (follow-up activity, retry, do-not-contact flags, lead conversion), so read the subject flow first when unsure. The subject's own form fields are not filled; follow-up dates use the subject actions' defaults.";

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

        var authorizationService = services.GetRequiredService<IAuthorizationService>();
        var mayCompleteOwn = await authorizationService.AuthorizeAsync(caller, OmnichannelConstants.Permissions.CompleteOwnActivity);

        if (!mayCompleteOwn && !await authorizationService.AuthorizeAsync(caller, OmnichannelConstants.Permissions.CompleteActivity))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var id = AIModuleTools.ReadString(arguments, "id");

        if (id is null)
        {
            return "Nothing was completed: give the ID of the activity in id.";
        }

        var activity = await services.GetRequiredService<IOmnichannelActivityManager>().FindByIdAsync(id, cancellationToken);

        if (activity is null)
        {
            return $"Nothing was completed: no activity has the ID '{id}'. Call listOmnichannelActivities to find it.";
        }

        // The same resource check as the Complete page: Complete activity, or Complete own activity on an activity
        // assigned to or reserved by the caller.
        if (!await authorizationService.AuthorizeAsync(caller, OmnichannelConstants.Permissions.CompleteActivity, activity))
        {
            if (!string.IsNullOrEmpty(activity.DialerProfileId) && mayCompleteOwn && !activity.Status.IsTerminal())
            {
                return "Nothing was completed: this record is no longer reserved for you. Its offer ran out and it went back to the queue for another agent.";
            }

            if (!string.IsNullOrEmpty(activity.DialerProfileId) && mayCompleteOwn)
            {
                return "Nothing was completed: this activity was already completed.";
            }

            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (activity.Status is ActivityStatus.Completed or ActivityStatus.Cancelled or ActivityStatus.Purged)
        {
            return "Nothing was completed: this activity was already completed, so the disposition was not recorded.";
        }

        var dispositionId = AIModuleTools.ReadString(arguments, "dispositionId");
        OmnichannelDisposition disposition = null;

        if (dispositionId is not null)
        {
            disposition = await SubjectDispositionRules.FindAllowedAsync(
                services.GetRequiredService<ISourceCatalog<SubjectAction>>(),
                services.GetRequiredService<INamedCatalog<OmnichannelDisposition>>(),
                activity,
                dispositionId);

            if (disposition is null)
            {
                return $"Nothing was completed: the disposition '{dispositionId}' is not one the subject '{activity.SubjectContentType}' offers. Its subject flow lists the dispositions it accepts.";
            }
        }

        var request = new ActivityDispositionRequest
        {
            Activity = activity,
            DispositionId = dispositionId,
            Notes = AIModuleTools.ReadString(arguments, "notes"),
            Source = ActivityDispositionSource.Agent,
            ActorId = caller.FindFirstValue(ClaimTypes.NameIdentifier),
            ActorDisplayName = await GetDisplayNameAsync(services, caller, cancellationToken),
        };
        var result = await services.GetRequiredService<IActivityDispositionService>().ApplyAsync(request, cancellationToken);

        if (!result.Succeeded)
        {
            return $"Nothing was completed: {result.ErrorMessage}";
        }

        // The follow-up activities the subject actions created, so the model can say what happens next.
        var followUps = new List<object>();

        foreach (var created in request.CreatedActivities?.Values ?? [])
        {
            if (created is not null)
            {
                followUps.Add(new
                {
                    id = created.ItemId,
                    subjectContentType = created.SubjectContentType,
                    scheduled = await AIModuleTools.ToLocalAsync(services.GetService<ILocalClock>(), created.ScheduledUtc),
                    assignedTo = created.AssignedToUsername,
                });
            }
        }

        return AIModuleTools.Json(new
        {
            completed = new
            {
                id = activity.ItemId,
                status = activity.Status.ToString(),
                disposition = disposition is null
                    ? null
                    : new
                    {
                        id = disposition.ItemId,
                        name = disposition.Name,
                    },
            },
            followUpActivities = followUps,
        });
    }

    private static async Task<string> GetDisplayNameAsync(IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var userManager = services.GetService<UserManager<IUser>>();
        var displayNameProvider = services.GetService<IDisplayNameProvider>();
        var user = userManager is null ? null : await userManager.GetUserAsync(caller);

        if (user is not null && displayNameProvider is not null)
        {
            var displayName = await displayNameProvider.GetAsync(user, cancellationToken);

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }
        }

        return caller.Identity?.Name;
    }
}
