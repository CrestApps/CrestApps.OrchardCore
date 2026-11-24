using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;

namespace CrestApps.OrchardCore.Dispatch.Mobile;

/// <summary>
/// The technician app's endpoints: the pack of visits to keep offline, the sync of the outbox, location consent
/// and positions. The My Work page calls them under <c>my-work/field/api</c> with its sign-in and an antiforgery
/// header; other apps call them under <c>api/dispatch/v1/mobile</c> with the API scheme.
/// </summary>
internal static class FieldEndpoints
{
    public const string PagePrefix = "my-work/field/api";

    public const string ApiPrefix = "api/dispatch/v1/mobile";

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static IEndpointRouteBuilder MapFieldEndpoints(this IEndpointRouteBuilder routes)
    {
        var page = routes.MapGroup(PagePrefix)
            .RequireAuthorization()
            .DisableAntiforgery()
            .AddEndpointFilter(ValidateAntiforgeryAsync)
            .ExcludeFromDescription();

        page.MapGet("token", (Delegate)Token);
        Map(page);

        var api = routes.MapGroup(ApiPrefix)
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = OrchardCoreConstants.AuthenticationSchemes.Api })
            .DisableAntiforgery()
            .WithTags("Dispatch");

        Map(api);

        return routes;
    }

    private static void Map(RouteGroupBuilder group)
    {
        group.MapGet("pack", (Delegate)PackAsync).WithSummary("Gets the caller's visits from today, with their work orders, to keep offline.");
        group.MapPost("sync", (Delegate)SyncAsync).WithSummary("Applies the caller's outbox of commands in order, each once by its key, with a result for each.");
        group.MapPost("consent", (Delegate)ConsentAsync).WithSummary("Records that the caller agreed to share their location while working, or withdrew.");
        group.MapPost("pings", (Delegate)PingsAsync).WithSummary("Keeps the caller's positions while they work, when they agreed.");
    }

    internal static async Task<IResult> PackAsync(HttpContext httpContext)
    {
        var (worker, denied) = await WorkerAsync(httpContext);

        if (denied is not null)
        {
            return denied;
        }

        var pack = await httpContext.RequestServices.GetRequiredService<IFieldSyncService>().GetPackAsync(worker.ItemId, httpContext.RequestAborted);

        return Results.Json(pack, JsonOptions);
    }

    internal static async Task<IResult> SyncAsync(HttpContext httpContext)
    {
        var (worker, denied) = await WorkerAsync(httpContext);

        if (denied is not null)
        {
            return denied;
        }

        var request = await ReadAsync<FieldSyncRequest>(httpContext);

        if (request?.Commands is null)
        {
            return Results.BadRequest();
        }

        var services = httpContext.RequestServices;
        var results = await services.GetRequiredService<IFieldSyncService>().ApplyAsync(worker.ItemId, request.Commands, httpContext.User.Identity?.Name ?? worker.DisplayName, httpContext.RequestAborted);

        await services.GetRequiredService<global::YesSql.ISession>().SaveChangesAsync(httpContext.RequestAborted);

        return Results.Json(new { results }, JsonOptions);
    }

    internal static async Task<IResult> ConsentAsync(HttpContext httpContext)
    {
        var (worker, denied) = await WorkerAsync(httpContext);

        if (denied is not null)
        {
            return denied;
        }

        var request = await ReadAsync<FieldConsentRequest>(httpContext);

        if (request is null)
        {
            return Results.BadRequest();
        }

        var services = httpContext.RequestServices;
        var consent = await services.GetRequiredService<IWorkerLocationService>().SetConsentAsync(worker.ItemId, request.Given, httpContext.RequestAborted);

        await services.GetRequiredService<global::YesSql.ISession>().SaveChangesAsync(httpContext.RequestAborted);

        return Results.Json(new { consented = consent.NoticeVersion is not null, noticeVersion = consent.NoticeVersion }, JsonOptions);
    }

    internal static async Task<IResult> PingsAsync(HttpContext httpContext)
    {
        var (worker, denied) = await WorkerAsync(httpContext);

        if (denied is not null)
        {
            return denied;
        }

        var request = await ReadAsync<FieldPingsRequest>(httpContext);

        if (request?.Pings is null)
        {
            return Results.BadRequest();
        }

        var services = httpContext.RequestServices;
        var result = await services.GetRequiredService<IWorkerLocationService>().RecordAsync(worker.ItemId, request.Pings, httpContext.RequestAborted);

        await services.GetRequiredService<global::YesSql.ISession>().SaveChangesAsync(httpContext.RequestAborted);

        return Results.Json(result, JsonOptions);
    }

    // The page asks for a token before each sync, so a page kept offline overnight still sends a fresh one.
    internal static IResult Token(HttpContext httpContext)
    {
        var tokens = httpContext.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(httpContext);

        httpContext.Response.Headers.CacheControl = "no-store";

        return Results.Json(new { header = tokens.HeaderName, token = tokens.RequestToken }, JsonOptions);
    }

    private static async ValueTask<object> ValidateAntiforgeryAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        if (HttpMethods.IsPost(httpContext.Request.Method) && !await httpContext.RequestServices.GetRequiredService<IAntiforgery>().IsRequestValidAsync(httpContext))
        {
            return Results.BadRequest();
        }

        return await next(context);
    }

    private static async Task<(StaffMember Worker, IResult Denied)> WorkerAsync(HttpContext httpContext)
    {
        var services = httpContext.RequestServices;

        if (!await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(httpContext.User, WorkforcePermissions.UseWorkforcePortal))
        {
            return (null, Results.Forbid());
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var worker = string.IsNullOrEmpty(userId) ? null : await services.GetRequiredService<IStaffMemberManager>().FindByUserIdAsync(userId, httpContext.RequestAborted);

        return worker is null ? (null, Results.NotFound()) : (worker, null);
    }

    private static async Task<T> ReadAsync<T>(HttpContext httpContext)
        where T : class
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(httpContext.Request.Body, JsonOptions, httpContext.RequestAborted);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// The body of a sync.
/// </summary>
internal sealed class FieldSyncRequest
{
    public List<FieldCommand> Commands { get; set; }
}

/// <summary>
/// The body of a consent change.
/// </summary>
internal sealed class FieldConsentRequest
{
    public bool Given { get; set; }
}

/// <summary>
/// The body of positions sent.
/// </summary>
internal sealed class FieldPingsRequest
{
    public List<LocationPingInput> Pings { get; set; }
}
