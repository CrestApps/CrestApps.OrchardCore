using System.Text.RegularExpressions;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Endpoints.Api;

/// <summary>
/// AI calls and texts over the API: start one, start a batch, read where one or a batch stands, and cancel one that
/// has not started. Each is an automated activity, so it goes through the same opt-out, do-not-call and calling-window
/// checks as any other.
/// </summary>
internal static partial class AIActivitiesApiEndpoints
{
    public const string RoutePrefix = "api/ai/v1";

    public const string Tag = "AI Calls and Texts";

    public static IEndpointRouteBuilder MapAIActivitiesApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutePrefix)
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = AIApiKeys.ApiSchemes })
            .DisableAntiforgery()
            .WithTags(Tag);

        group.MapPost("calls", (ApiAIActivityRequest request, HttpContext httpContext, IAuthorizationService authorizationService, IAIProfileManager profileManager, AIActivityStarter starter)
                => StartAsync(OmnichannelConstants.Channels.Phone, request, httpContext, authorizationService, profileManager, starter))
            .WithName("StartAICall")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .WithSummary("Starts an AI phone call to one customer, now or at a set time.");

        group.MapPost("texts", (ApiAIActivityRequest request, HttpContext httpContext, IAuthorizationService authorizationService, IAIProfileManager profileManager, AIActivityStarter starter)
                => StartAsync(OmnichannelConstants.Channels.Sms, request, httpContext, authorizationService, profileManager, starter))
            .WithName("StartAIText")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .WithSummary("Starts an AI text conversation with one customer: the AI sends the first message.");

        group.MapPost("batches", StartBatchAsync)
            .WithName("StartAIActivityBatch")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .WithSummary("Starts AI calls or texts with up to 1,000 customers, each with its own variables.");

        group.MapGet("batches/{batchId}", GetBatchAsync)
            .WithName("GetAIActivityBatch")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets where each call or text of a batch stands.");

        group.MapGet("activities/{activityId}", GetAsync)
            .WithName("GetAIActivity")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets where an AI call or text stands, and its conversation once it started.");

        group.MapPost("activities/{activityId}/cancel", CancelAsync)
            .WithName("CancelAIActivity")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Cancels an AI call or text that has not started. End a live call with the live call endpoints.");

        return routes;
    }

    internal static async Task<Results<Created<ApiAIActivity>, ValidationProblem, ProblemHttpResult>> StartAsync(
        string channel,
        ApiAIActivityRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAIProfileManager profileManager,
        AIActivityStarter starter)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Calls, OmnichannelConstants.Permissions.EditActivity))
        {
            return Forbidden();
        }

        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request?.ProfileId) || await profileManager.FindByIdAsync(request.ProfileId, httpContext.RequestAborted) is null)
        {
            errors["profileId"] = ["Name an AI profile that exists."];
        }

        if (!IsPhoneNumber(request?.To))
        {
            errors["to"] = ["Give the phone number in E.164 form, such as +15551234567."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var activity = await starter.StartAsync(new AIActivityStartRequest
        {
            Channel = channel,
            AIProfileId = request.ProfileId,
            Destination = request.To,
            ChannelEndpointId = request.FromEndpointId,
            CampaignId = request.CampaignId,
            SubjectContentType = request.SubjectContentType,
            ContactContentItemId = request.ContactId,
            Variables = ToVariables(request.Variables),
            Source = ActivitySources.Api,
            ScheduledUtc = request.ScheduledUtc?.ToUniversalTime(),
        });

        return TypedResults.Created($"/{RoutePrefix}/activities/{activity.ItemId}", ApiAIActivity.From(activity));
    }

    private static async Task<Results<Created<ApiAIActivityBatch>, ValidationProblem, ProblemHttpResult>> StartBatchAsync(
        ApiAIActivityBatchRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAIProfileManager profileManager,
        AIActivityStarter starter,
        ISession session,
        IClock clock)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Calls, OmnichannelConstants.Permissions.EditActivity))
        {
            return Forbidden();
        }

        var errors = new Dictionary<string, string[]>();
        var channel = request?.Channel?.Trim().ToLowerInvariant() switch
        {
            null or "" or "call" or "phone" => OmnichannelConstants.Channels.Phone,
            "text" or "sms" => OmnichannelConstants.Channels.Sms,
            _ => null,
        };

        if (channel is null)
        {
            errors["channel"] = ["Use call or text."];
        }

        if (string.IsNullOrWhiteSpace(request?.ProfileId) || await profileManager.FindByIdAsync(request.ProfileId, httpContext.RequestAborted) is null)
        {
            errors["profileId"] = ["Name an AI profile that exists."];
        }

        var items = request?.Items ?? [];

        if (items.Count == 0 || items.Count > ApiAIActivityBatchRequest.MaxItems)
        {
            errors["items"] = [$"Give between 1 and {ApiAIActivityBatchRequest.MaxItems} customers."];
        }

        var badNumbers = items.Select((item, index) => (item, index)).Where(pair => !IsPhoneNumber(pair.item?.To)).Select(pair => pair.index).Take(10).ToList();

        if (badNumbers.Count > 0)
        {
            errors["items.to"] = [$"Give each phone number in E.164 form. Not valid at: {string.Join(", ", badNumbers)}."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var batch = new AIApiActivityBatch { ProfileId = request.ProfileId, CreatedUtc = clock.UtcNow };
        var activities = new List<OmnichannelActivity>(items.Count);

        foreach (var item in items)
        {
            var activity = await starter.StartAsync(new AIActivityStartRequest
            {
                Channel = channel,
                AIProfileId = request.ProfileId,
                Destination = item.To,
                ChannelEndpointId = request.FromEndpointId,
                CampaignId = request.CampaignId,
                ContactContentItemId = item.ContactId,
                Variables = ToVariables(item.Variables),
                Source = ActivitySources.Api,
                ScheduledUtc = request.ScheduledUtc?.ToUniversalTime(),
            });

            activities.Add(activity);
            batch.ActivityIds.Add(activity.ItemId);
        }

        await session.SaveAsync(batch);
        await session.SaveChangesAsync(httpContext.RequestAborted);

        return TypedResults.Created($"/{RoutePrefix}/batches/{batch.Id}", ToBatch(batch, activities));
    }

    private static async Task<Results<Ok<ApiAIActivityBatch>, ProblemHttpResult>> GetBatchAsync(
        string batchId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IOmnichannelActivityManager activityManager,
        ISession session)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Calls, OmnichannelConstants.Permissions.ListActivities))
        {
            return Forbidden();
        }

        var batch = long.TryParse(batchId, out var id) ? (await session.GetAsync<AIApiActivityBatch>([id])).FirstOrDefault() : null;

        if (batch is null)
        {
            return NotFound();
        }

        var activities = new List<OmnichannelActivity>(batch.ActivityIds.Count);

        foreach (var activityId in batch.ActivityIds)
        {
            if (await activityManager.FindByIdAsync(activityId, httpContext.RequestAborted) is { } activity)
            {
                activities.Add(activity);
            }
        }

        return TypedResults.Ok(ToBatch(batch, activities));
    }

    private static async Task<Results<Ok<ApiAIActivity>, ProblemHttpResult>> GetAsync(
        string activityId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IOmnichannelActivityManager activityManager)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Calls, OmnichannelConstants.Permissions.ListActivities))
        {
            return Forbidden();
        }

        var activity = await activityManager.FindByIdAsync(activityId, httpContext.RequestAborted);

        return activity is null || string.IsNullOrEmpty(activity.AIProfileId) ? NotFound() : TypedResults.Ok(ApiAIActivity.From(activity));
    }

    private static async Task<Results<Ok<ApiAIActivity>, ProblemHttpResult>> CancelAsync(
        string activityId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IOmnichannelActivityManager activityManager,
        IClock clock)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Calls, OmnichannelConstants.Permissions.EditActivity))
        {
            return Forbidden();
        }

        var activity = await activityManager.FindByIdAsync(activityId, httpContext.RequestAborted);

        if (activity is null || string.IsNullOrEmpty(activity.AIProfileId))
        {
            return NotFound();
        }

        if (!CanCancel(activity.Status))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: $"The {activity.Kind.ToString().ToLowerInvariant()} is {activity.Status} and can no longer be cancelled.");
        }

        activity.Status = ActivityStatus.Cancelled;
        activity.CompletedUtc = clock.UtcNow;
        await activityManager.UpdateAsync(activity, cancellationToken: httpContext.RequestAborted);

        return TypedResults.Ok(ApiAIActivity.From(activity));
    }

    internal static bool CanCancel(ActivityStatus status)
        => status is ActivityStatus.NotStated or ActivityStatus.Pending or ActivityStatus.Scheduled;

    internal static bool IsPhoneNumber(string value)
        => !string.IsNullOrWhiteSpace(value) && PhoneNumberPattern().IsMatch(value.Trim());

    private static List<KeyValuePair<string, string>> ToVariables(Dictionary<string, string> variables)
        => (variables ?? [])
            .Where(variable => !string.IsNullOrWhiteSpace(variable.Key) && !string.IsNullOrWhiteSpace(variable.Value))
            .Select(variable => new KeyValuePair<string, string>(variable.Key.Trim(), variable.Value.Trim()))
            .ToList();

    private static ApiAIActivityBatch ToBatch(AIApiActivityBatch batch, IReadOnlyList<OmnichannelActivity> activities)
        => new(
            batch.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            batch.CreatedUtc,
            activities.GroupBy(activity => activity.Status.ToString()).ToDictionary(group => group.Key, group => group.Count()),
            activities.Select(ApiAIActivity.From).ToList());

    private static ProblemHttpResult Forbidden()
        => TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");

    private static ProblemHttpResult NotFound()
        => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found");

    [GeneratedRegex(@"^\+[1-9]\d{6,14}$")]
    private static partial Regex PhoneNumberPattern();
}
