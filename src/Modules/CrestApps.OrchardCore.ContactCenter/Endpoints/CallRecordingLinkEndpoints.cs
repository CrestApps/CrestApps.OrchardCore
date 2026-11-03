using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.ContactCenter.Controllers;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

/// <summary>
/// Signed, expiring links to recordings: the audio behind a link, and, over the AI API, listing a call's recordings and
/// making a link to one.
/// </summary>
internal static class CallRecordingLinkEndpoints
{
    public static IEndpointRouteBuilder MapCallRecordingLinks(this IEndpointRouteBuilder routes)
    {
        // The token is the credential: anyone holding an unexpired link may play the recording.
        routes.MapGet(CallRecordingLinks.RoutePrefix + "/{token}", PlayAsync)
            .AllowAnonymous()
            .ExcludeFromDescription();

        return routes;
    }

    public static IEndpointRouteBuilder MapCallRecordingsApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("api/ai/v1/recordings")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = AIApiKeys.ApiSchemes })
            .DisableAntiforgery()
            .WithTags("AI Calls and Texts");

        group.MapGet(string.Empty, ListAsync)
            .WithName("ListCallRecordings")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .WithSummary("Lists the recordings of an activity's calls.");

        group.MapPost("{recordingId}/links", CreateLinkAsync)
            .WithName("CreateCallRecordingLink")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Makes a link to a recording's audio that works without signing in until it expires (24 hours unless set, at most 7 days).");

        return routes;
    }

    private static async Task<IResult> PlayAsync(
        string token,
        HttpContext httpContext,
        CallRecordingLinks links,
        ICallRecordingStore recordingStore,
        IEnumerable<IRecordingMediaStore> mediaStores,
        ILoggerFactory loggerFactory)
    {
        var recordingId = links.Read(token);
        var recording = recordingId is null ? null : await recordingStore.FindByIdAsync(recordingId, httpContext.RequestAborted);
        var mediaStore = mediaStores.LastOrDefault();

        if (recording is null || recording.ErasedUtc is not null || string.IsNullOrEmpty(recording.StorageReference) || mediaStore is null)
        {
            return TypedResults.NotFound();
        }

        var source = await mediaStore.OpenReadAsync(recording.StorageReference, httpContext.RequestAborted);

        if (source is null)
        {
            return TypedResults.NotFound();
        }

        // Only the start of a playback is logged, as the admin player audits it.
        if (string.IsNullOrEmpty(httpContext.Request.Headers.Range) && loggerFactory.CreateLogger(typeof(CallRecordingLinkEndpoints)) is { } logger && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Call recording '{RecordingId}' was played from a shared link.", recording.ItemId.SanitizeLogValue());
        }

        httpContext.Response.Headers.CacheControl = "private, no-store";
        httpContext.Response.Headers["X-Robots-Tag"] = "noindex";

        return TypedResults.File(source, CallRecordingsController.ContentTypeOf(recording.Format), enableRangeProcessing: true);
    }

    private static async Task<Results<Ok<IReadOnlyList<ApiCallRecording>>, ValidationProblem, ProblemHttpResult>> ListAsync(
        string activityId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        ISession session)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Recordings, ContactCenterPermissions.ListenToAllCallRecordings))
        {
            return Forbidden();
        }

        if (string.IsNullOrWhiteSpace(activityId))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["activityId"] = ["Name the activity whose recordings to list."] });
        }

        var recordings = await session.Query<CallRecording, CallRecordingIndex>(
                index => index.ActivityItemId == activityId && index.IsStored && !index.IsErased,
                collection: ContactCenterStorage.CollectionName)
            .OrderBy(index => index.StartedUtc)
            .ListAsync(httpContext.RequestAborted);

        return TypedResults.Ok<IReadOnlyList<ApiCallRecording>>(recordings.Select(recording => new ApiCallRecording(
            recording.ItemId,
            recording.ActivityItemId,
            recording.AiSessionId,
            recording.StartedUtc,
            recording.DurationSeconds,
            recording.Format,
            recording.TryGet<CallRecordingBleepMetadata>(out var bleep) && bleep.Bleeps > 0)).ToList());
    }

    private static async Task<Results<Ok<ApiCallRecordingLink>, ProblemHttpResult>> CreateLinkAsync(
        string recordingId,
        int? minutes,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        ICallRecordingStore recordingStore,
        CallRecordingLinks links)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Recordings, ContactCenterPermissions.ListenToAllCallRecordings))
        {
            return Forbidden();
        }

        var recording = await recordingStore.FindByIdAsync(recordingId, httpContext.RequestAborted);

        if (recording is null || recording.ErasedUtc is not null || string.IsNullOrEmpty(recording.StorageReference))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found");
        }

        var (token, expires) = links.Create(recording.ItemId, minutes is > 0 ? TimeSpan.FromMinutes(minutes.Value) : null);

        return TypedResults.Ok(new ApiCallRecordingLink(BuildUrl(httpContext.Request, token), expires));
    }

    internal static string BuildUrl(HttpRequest request, string token)
        => $"{request.Scheme}://{request.Host}{request.PathBase}/{CallRecordingLinks.RoutePrefix}/{token}";

    private static ProblemHttpResult Forbidden()
        => TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
}

/// <summary>
/// A call recording.
/// </summary>
/// <param name="Id">The recording.</param>
/// <param name="ActivityId">The call's activity.</param>
/// <param name="ConversationId">The AI conversation, when an AI was on the call.</param>
/// <param name="StartedUtc">When it started.</param>
/// <param name="DurationSeconds">How long it is.</param>
/// <param name="Format">Its audio format.</param>
/// <param name="Bleeped">Whether personal details in it were covered with a tone.</param>
public sealed record ApiCallRecording(string Id, string ActivityId, string ConversationId, DateTime StartedUtc, double DurationSeconds, string Format, bool Bleeped);

/// <summary>
/// A signed link to a recording.
/// </summary>
/// <param name="Url">The link.</param>
/// <param name="ExpiresUtc">When it stops working.</param>
public sealed record ApiCallRecordingLink(string Url, DateTime ExpiresUtc);
