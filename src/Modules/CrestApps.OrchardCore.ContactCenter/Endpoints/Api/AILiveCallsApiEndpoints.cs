using CrestApps.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints.Api;

/// <summary>
/// Live AI calls over the API: list the calls the AI is holding, send the AI a note, transfer a call (cold, warm or
/// screened) to a number or an agent, and end one. The same actions as the supervisor dashboard.
/// </summary>
internal static class AILiveCallsApiEndpoints
{
    private const int MaxLiveCalls = 200;

    public static IEndpointRouteBuilder MapAILiveCallsApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("api/ai/v1/live-calls")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = AIApiKeys.ApiSchemes })
            .DisableAntiforgery()
            .WithTags("AI Calls and Texts");

        group.MapGet(string.Empty, ListAsync)
            .WithName("ListAILiveCalls")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Lists the phone calls an AI is holding right now.");

        group.MapPost("{activityId}/notes", NoteAsync)
            .WithName("SendAILiveCallNote")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .WithSummary("Sends the AI on a live call a note it acts on at its next turn; the caller does not hear it.");

        group.MapPost("{activityId}/transfer", TransferAsync)
            .WithName("TransferAILiveCall")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .WithSummary("Transfers a live AI call to a phone number or an agent: cold, warm (the AI briefs the person first) or screened.");

        group.MapPost("{activityId}/end", EndAsync)
            .WithName("EndAILiveCall")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Hangs up a live AI call.");

        return routes;
    }

    private static async Task<Results<Ok<IReadOnlyList<ApiAILiveCall>>, ProblemHttpResult>> ListAsync(
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        ISession session,
        IClock clock)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Calls, ContactCenterPermissions.MonitorContactCenter))
        {
            return Forbidden();
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
        var calls = new List<ApiAILiveCall>();

        foreach (var activity in activities)
        {
            if (activity.TryGet<AILiveCall>(out var liveCall))
            {
                calls.Add(new ApiAILiveCall(
                    activity.ItemId,
                    activity.AIProfileId,
                    activity.PreferredDestination,
                    activity.ContactContentItemId,
                    activity.AISessionId,
                    string.Equals(activity.Source, ActivitySources.Inbound, StringComparison.Ordinal) ? "Inbound" : "Outbound",
                    liveCall.AnsweredUtc,
                    (int)Math.Max(0, (now - liveCall.AnsweredUtc).TotalSeconds),
                    liveCall.IsRealtime));
            }
        }

        return TypedResults.Ok<IReadOnlyList<ApiAILiveCall>>(calls);
    }

    private static async Task<Results<Ok<ApiAILiveCallResult>, ValidationProblem, ProblemHttpResult>> NoteAsync(
        string activityId,
        ApiAILiveCallNote request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IEnumerable<IAIVoiceCallSupervision> supervisions)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Calls, ContactCenterPermissions.InterveneInCalls))
        {
            return Forbidden();
        }

        if (string.IsNullOrWhiteSpace(request?.Note))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["note"] = ["Write the note."] });
        }

        var supervision = supervisions.FirstOrDefault();
        var sent = supervision is not null && await supervision.SendNoteAsync(activityId, request.Note.Trim(), httpContext.RequestAborted);

        return TypedResults.Ok(new ApiAILiveCallResult(sent, sent ? "The AI got the note." : "No AI is on this call."));
    }

    private static async Task<Results<Ok<ApiAILiveCallResult>, ValidationProblem, ProblemHttpResult>> TransferAsync(
        string activityId,
        ApiAILiveCallTransfer request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IEnumerable<IAIVoiceCallTransfers> transfers)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Calls, ContactCenterPermissions.InterveneInCalls))
        {
            return Forbidden();
        }

        var errors = new Dictionary<string, string[]>();
        var hasNumber = !string.IsNullOrWhiteSpace(request?.To);
        var hasAgent = !string.IsNullOrWhiteSpace(request?.AgentId);

        if (hasNumber == hasAgent)
        {
            errors["to"] = ["Give either a phone number or an agent."];
        }

        if (!Enum.TryParse<AICallTransferMode>(string.IsNullOrWhiteSpace(request?.Mode) ? nameof(AICallTransferMode.Cold) : request.Mode, ignoreCase: true, out var mode))
        {
            errors["mode"] = ["Use cold, warm or screened."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        // A number outside the contact center needs the right to transfer calls out.
        if (hasNumber && !await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Calls, ContactCenterPermissions.TransferExternally))
        {
            return Forbidden();
        }

        var service = transfers.FirstOrDefault();

        if (service is null)
        {
            return TypedResults.Ok(new ApiAILiveCallResult(false, "Transfers are not available."));
        }

        var result = await service.TransferAsync(activityId, new AITakeOverRequest
        {
            TargetPhoneNumber = hasNumber ? request.To.Trim() : null,
            TargetAgentId = hasAgent ? request.AgentId.Trim() : null,
            TransferMode = mode,
            Briefing = request.Briefing,
            Introduction = request.Introduction,
            RingTimeoutSeconds = Math.Clamp(request.RingTimeoutSeconds ?? 30, 5, 120),
            ShowCallerNumber = request.ShowCallerNumber,
            RequestedBy = httpContext.User.Identity?.Name,
        }, httpContext.RequestAborted);

        return TypedResults.Ok(new ApiAILiveCallResult(result.Succeeded, result.Message));
    }

    private static async Task<Results<Ok<ApiAILiveCallResult>, ProblemHttpResult>> EndAsync(
        string activityId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IEnumerable<IAIVoiceCallSupervision> supervisions)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Calls, ContactCenterPermissions.InterveneInCalls))
        {
            return Forbidden();
        }

        var supervision = supervisions.FirstOrDefault();

        if (supervision is null)
        {
            return TypedResults.Ok(new ApiAILiveCallResult(false, "No AI is on this call."));
        }

        var result = await supervision.EndAsync(activityId, httpContext.RequestAborted);

        return TypedResults.Ok(new ApiAILiveCallResult(result.Succeeded, result.Message));
    }

    private static ProblemHttpResult Forbidden()
        => TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
}

/// <summary>
/// A phone call an AI is holding.
/// </summary>
/// <param name="ActivityId">The call's activity.</param>
/// <param name="ProfileId">The AI profile.</param>
/// <param name="CustomerNumber">The customer's number.</param>
/// <param name="ContactId">The contact, when known.</param>
/// <param name="ConversationId">The AI conversation; its transcript grows in the conversations API.</param>
/// <param name="Direction">Inbound or Outbound.</param>
/// <param name="AnsweredUtc">When it was answered.</param>
/// <param name="DurationSeconds">How long it has gone on.</param>
/// <param name="IsRealtime">Whether a realtime voice model holds it.</param>
public sealed record ApiAILiveCall(
    string ActivityId,
    string ProfileId,
    string CustomerNumber,
    string ContactId,
    string ConversationId,
    string Direction,
    DateTime AnsweredUtc,
    int DurationSeconds,
    bool IsRealtime);

/// <summary>
/// What came of an action on a live call.
/// </summary>
/// <param name="Succeeded">Whether it was done.</param>
/// <param name="Message">What happened.</param>
public sealed record ApiAILiveCallResult(bool Succeeded, string Message);

/// <summary>
/// A note for the AI on a live call.
/// </summary>
public sealed class ApiAILiveCallNote
{
    /// <summary>
    /// Gets or sets the note.
    /// </summary>
    public string Note { get; set; }
}

/// <summary>
/// Where to transfer a live AI call, and how.
/// </summary>
public sealed class ApiAILiveCallTransfer
{
    /// <summary>
    /// Gets or sets the phone number to transfer to.
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets the agent profile to transfer to.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets cold, warm or screened. Defaults to cold.
    /// </summary>
    public string Mode { get; set; }

    /// <summary>
    /// Gets or sets what the AI tells the person before connecting, for warm and screened transfers. Empty lets the AI
    /// write it from the conversation.
    /// </summary>
    public string Briefing { get; set; }

    /// <summary>
    /// Gets or sets what the AI tells the caller before the transfer.
    /// </summary>
    public string Introduction { get; set; }

    /// <summary>
    /// Gets or sets how long to ring before going back to the caller, from 5 to 120 seconds. Defaults to 30.
    /// </summary>
    public int? RingTimeoutSeconds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the person sees the caller's number rather than the business's.
    /// </summary>
    public bool ShowCallerNumber { get; set; }
}
