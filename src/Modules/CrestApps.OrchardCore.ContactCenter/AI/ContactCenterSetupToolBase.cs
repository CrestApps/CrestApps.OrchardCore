using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// A Contact Center setup AI tool: it reads or changes the configuration records the Contact Center admin pages manage
/// (queues, entry points, business hours, agents, reason codes and dialer profiles). It acts as the person using the AI,
/// so it refuses a caller who is not signed in or lacks the permission the matching admin page checks.
/// </summary>
internal abstract class ContactCenterSetupToolBase : AIFunction
{
    /// <summary>
    /// The answer to a caller who is not signed in or lacks the permission.
    /// </summary>
    public const string NotAllowedMessage = "You are not allowed to use this function.";

    /// <summary>
    /// The page size a list returns when none is asked for.
    /// </summary>
    public const int DefaultPageSize = 20;

    /// <summary>
    /// The largest page size a list returns.
    /// </summary>
    public const int MaxPageSize = 50;

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <summary>
    /// Gets the permission the caller needs before the tool runs at all, or <see langword="null"/> when the tool checks
    /// its permission itself because it depends on an argument.
    /// </summary>
    protected abstract Permission Permission { get; }

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var user = GetCaller(arguments.Services);

        if (user is null)
        {
            return NotAllowedMessage;
        }

        if (Permission is not null && !await IsAuthorizedAsync(arguments.Services, user, Permission))
        {
            return NotAllowedMessage;
        }

        return await InvokeAsync(arguments, arguments.Services, user, cancellationToken);
    }

    /// <summary>
    /// Runs the tool for a signed-in caller who holds the tool's permission.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="user">The person using the AI.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The tool's result.</returns>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the signed-in caller. The chat hubs publish the caller through <see cref="IUserAccessor"/> for the whole
    /// invocation, which also works on transports where the request is not available to the hub method; the request's
    /// user is the fallback.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The caller, or <see langword="null"/> when nobody is signed in.</returns>
    internal static ClaimsPrincipal GetCaller(IServiceProvider services)
    {
        var user = services.GetService<IUserAccessor>()?.User
            ?? services.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        return user?.Identity?.IsAuthenticated == true
            ? user
            : null;
    }

    /// <summary>
    /// Checks a permission for the caller, with no resource, as the Contact Center admin pages do.
    /// </summary>
    /// <returns><see langword="true"/> when the caller holds the permission.</returns>
    internal static async Task<bool> IsAuthorizedAsync(IServiceProvider services, ClaimsPrincipal user, Permission permission)
    {
        var authorizationService = services.GetService<IAuthorizationService>();

        return user is not null &&
            authorizationService is not null &&
            await authorizationService.AuthorizeAsync(user, permission);
    }

    /// <summary>
    /// Parses a JSON schema.
    /// </summary>
    protected static JsonElement Schema(string schema)
        => JsonSerializer.Deserialize<JsonElement>(schema);

    /// <summary>
    /// Writes a result as JSON.
    /// </summary>
    internal static string Json(object value)
        => JsonSerializer.Serialize(value, _jsonOptions);

    /// <summary>
    /// Reads the <c>page</c> and <c>pageSize</c> arguments, with the page size capped.
    /// </summary>
    internal static (int Page, int PageSize) ReadPaging(ContactCenterToolArguments arguments)
    {
        var page = Math.Max(1, arguments.GetInt("page") ?? 1);
        var pageSize = Math.Clamp(arguments.GetInt("pageSize") ?? DefaultPageSize, 1, MaxPageSize);

        return (page, pageSize);
    }

    /// <summary>
    /// Converts a stored UTC time to the user's or the site's time zone.
    /// </summary>
    /// <returns>The local time with its offset, or <see langword="null"/>.</returns>
    internal static async Task<string> ToLocalAsync(ILocalClock localClock, DateTime? utc)
    {
        if (utc is null || utc.Value == default || localClock is null)
        {
            return null;
        }

        var local = await localClock.ConvertToLocalAsync(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc));

        return local.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets the path of a Contact Center admin page, or <see langword="null"/> when there is no request to build it from.
    /// </summary>
    protected static string GetAdminPath(IServiceProvider services, string controller, string action, string id = null)
    {
        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;
        var linkGenerator = services.GetService<LinkGenerator>();

        if (httpContext is null || linkGenerator is null)
        {
            return null;
        }

        var values = new RouteValueDictionary
        {
            ["area"] = typeof(ContactCenterSetupToolBase).Assembly.GetName().Name,
            ["controller"] = controller,
            ["action"] = action,
        };

        if (id is not null)
        {
            values["id"] = id;
        }

        return linkGenerator.GetPathByRouteValues(httpContext, null, values);
    }
}
