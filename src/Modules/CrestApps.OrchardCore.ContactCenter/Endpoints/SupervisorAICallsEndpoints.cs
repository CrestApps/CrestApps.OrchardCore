using System.Security.Claims;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

/// <summary>
/// The supervisor dashboard's live AI calls: the calls the AI is holding right now, each one's transcript as it
/// grows, and what a supervisor can do to one (send the AI a note, take the call over, end it).
/// </summary>
internal static class SupervisorAICallsEndpoints
{
    private const int MaxLiveCalls = 200;

    public const string ListRouteName = "ContactCenterSupervisorAICalls";
    public const string TranscriptRouteName = "ContactCenterSupervisorAICallTranscript";
    public const string NoteRouteName = "ContactCenterSupervisorAICallNote";
    public const string TakeOverRouteName = "ContactCenterSupervisorAICallTakeOver";
    public const string EndRouteName = "ContactCenterSupervisorAICallEnd";
    public const string ListenRouteName = "ContactCenterSupervisorAICallListen";

    public static IEndpointRouteBuilder AddSupervisorAICallsEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("Admin/contact-center/dashboard/ai-calls", HandleListAsync).WithName(ListRouteName);
        builder.MapGet("Admin/contact-center/dashboard/ai-calls/{activityId}/transcript", HandleTranscriptAsync).WithName(TranscriptRouteName);
        builder.MapPost("Admin/contact-center/dashboard/ai-calls/{activityId}/note", HandleNoteAsync).WithName(NoteRouteName);
        builder.MapPost("Admin/contact-center/dashboard/ai-calls/{activityId}/take-over", HandleTakeOverAsync).WithName(TakeOverRouteName);
        builder.MapPost("Admin/contact-center/dashboard/ai-calls/{activityId}/end", HandleEndAsync).WithName(EndRouteName);
        builder.MapPost("Admin/contact-center/dashboard/ai-calls/{activityId}/listen", HandleListenAsync).WithName(ListenRouteName);

        return builder;
    }

    internal static async Task<IResult> HandleListAsync(
        IAuthorizationService authorizationService,
        ISession session,
        IAIProfileManager profileManager,
        IContentManager contentManager,
        IClock clock,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.MonitorContactCenter))
        {
            return TypedResults.Forbid();
        }

        var activities = await session
            .Query<OmnichannelActivity, OmnichannelActivityIndex>(
                index => index.InteractionType == ActivityInteractionType.Automated &&
                    index.Status == ActivityStatus.InProgress &&
                    index.Channel == OmnichannelConstants.Channels.Phone &&
                    !index.AiEscalated,
                collection: OmnichannelConstants.CollectionName)
            .OrderByDescending(index => index.ScheduledUtc)
            .Take(MaxLiveCalls)
            .ListAsync(httpContext.RequestAborted);

        var now = clock.UtcNow;
        var profileNames = new Dictionary<string, string>(StringComparer.Ordinal);
        var calls = new List<object>();

        foreach (var activity in activities)
        {
            if (!activity.TryGet<AILiveCall>(out var liveCall))
            {
                continue;
            }

            var profileName = await ResolveProfileNameAsync(profileManager, profileNames, activity.AIProfileId);
            var contact = string.IsNullOrEmpty(activity.ContactContentItemId)
                ? null
                : await contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

            calls.Add(new
            {
                ActivityId = activity.ItemId,
                ContactName = contact?.DisplayText,
                CustomerAddress = activity.PreferredDestination,
                ProfileName = profileName,
                Direction = string.Equals(activity.Source, ActivitySources.Inbound, StringComparison.Ordinal) ? "Inbound" : "Outbound",
                liveCall.AnsweredUtc,
                DurationSeconds = (int)Math.Max(0, (now - liveCall.AnsweredUtc).TotalSeconds),
                liveCall.IsRealtime,
            });
        }

        return TypedResults.Ok(new
        {
            ServerTimeUtc = now,
            CanIntervene = await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.InterveneInCalls),
            Calls = calls,
        });
    }

    internal static async Task<IResult> HandleTranscriptAsync(
        string activityId,
        int? after,
        IAuthorizationService authorizationService,
        IOmnichannelActivityStore activityStore,
        IAIChatSessionPromptStore promptStore,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.MonitorContactCenter))
        {
            return TypedResults.Forbid();
        }

        var activity = await activityStore.FindByIdAsync(activityId, httpContext.RequestAborted);

        if (activity is null || string.IsNullOrEmpty(activity.AISessionId))
        {
            return TypedResults.NotFound();
        }

        var turns = AIConversationTranscript.GetTurns(await promptStore.GetPromptsAsync(activity.AISessionId));
        var skip = Math.Clamp(after ?? 0, 0, turns.Count);

        return TypedResults.Ok(new
        {
            Total = turns.Count,
            Live = VoiceAgentConversationLoop.IsLiveAICall(activity),
            Turns = turns.Skip(skip).Select(turn => new
            {
                turn.Role,
                turn.Text,
                turn.CreatedUtc,
            }),
        });
    }

    internal static async Task<IResult> HandleNoteAsync(
        string activityId,
        [FromForm] NoteRequest request,
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        IEnumerable<IAIVoiceCallSupervision> supervisions,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.InterveneInCalls))
        {
            return TypedResults.Forbid();
        }

        if (!await ContactCenterEndpointAntiforgery.ValidateRequestAsync(antiforgery, httpContext) ||
            string.IsNullOrWhiteSpace(request?.Note))
        {
            return TypedResults.BadRequest();
        }

        var supervision = supervisions.FirstOrDefault();

        if (supervision is null)
        {
            return TypedResults.NotFound();
        }

        var sent = await supervision.SendNoteAsync(activityId, request.Note, httpContext.RequestAborted);

        return TypedResults.Ok(new
        {
            Succeeded = sent,
            Message = sent ? "The AI got your note." : "The AI is no longer on this call.",
        });
    }

    internal static async Task<IResult> HandleTakeOverAsync(
        string activityId,
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        IEnumerable<IAIVoiceCallSupervision> supervisions,
        IAgentProfileManager agentProfileManager,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.InterveneInCalls))
        {
            return TypedResults.Forbid();
        }

        if (!await ContactCenterEndpointAntiforgery.ValidateRequestAsync(antiforgery, httpContext))
        {
            return TypedResults.BadRequest();
        }

        var supervision = supervisions.FirstOrDefault();
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (supervision is null || string.IsNullOrEmpty(userId))
        {
            return TypedResults.NotFound();
        }

        // The call is rung through to the person's agent profile, as any call offered to them is.
        var agent = await agentProfileManager.FindByUserIdAsync(userId, httpContext.RequestAborted);
        var result = await supervision.TakeOverAsync(activityId, agent?.ItemId, httpContext.User.Identity?.Name, httpContext.RequestAborted);

        return TypedResults.Ok(new
        {
            result.Succeeded,
            result.Message,
        });
    }

    internal static async Task<IResult> HandleEndAsync(
        string activityId,
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        IEnumerable<IAIVoiceCallSupervision> supervisions,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.InterveneInCalls))
        {
            return TypedResults.Forbid();
        }

        if (!await ContactCenterEndpointAntiforgery.ValidateRequestAsync(antiforgery, httpContext))
        {
            return TypedResults.BadRequest();
        }

        var supervision = supervisions.FirstOrDefault();

        if (supervision is null)
        {
            return TypedResults.NotFound();
        }

        var result = await supervision.EndAsync(activityId, httpContext.RequestAborted);

        return TypedResults.Ok(new
        {
            result.Succeeded,
            result.Message,
        });
    }

    internal static async Task<IResult> HandleListenAsync(
        string activityId,
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        IOmnichannelActivityStore activityStore,
        IContactCenterPhoneCallSupervisionService phoneCalls,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.MonitorContactCenter))
        {
            return TypedResults.Forbid();
        }

        if (!await ContactCenterEndpointAntiforgery.ValidateRequestAsync(antiforgery, httpContext))
        {
            return TypedResults.BadRequest();
        }

        var activity = await activityStore.FindByIdAsync(activityId, httpContext.RequestAborted);
        var supervisorId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!VoiceAgentConversationLoop.IsLiveAICall(activity) || !activity.TryGet<AILiveCall>(out var liveCall) || string.IsNullOrEmpty(supervisorId))
        {
            return TypedResults.Ok(new { Succeeded = false, Message = "The AI is no longer on this call." });
        }

        // The supervisor's own soft phone rings with a listen-only leg on the customer's side of the call.
        var result = await phoneCalls.ListenToAICallAsync(activity.ItemId, liveCall.ProviderName, liveCall.ProviderCallId, supervisorId, httpContext.RequestAborted);

        return TypedResults.Ok(new
        {
            result.Succeeded,
            Message = result.Succeeded ? "Your phone is joining the call to listen." : result.Reason,
        });
    }

    private static async Task<string> ResolveProfileNameAsync(IAIProfileManager profileManager, Dictionary<string, string> cache, string profileId)
    {
        if (string.IsNullOrEmpty(profileId))
        {
            return null;
        }

        if (!cache.TryGetValue(profileId, out var name))
        {
            var profile = await profileManager.FindByIdAsync(profileId);
            cache[profileId] = name = profile?.DisplayText ?? profile?.Name;
        }

        return name;
    }

    internal sealed class NoteRequest
    {
        public string Note { get; set; }
    }
}
