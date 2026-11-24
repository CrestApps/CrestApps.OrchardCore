using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.AI;

/// <summary>
/// A messaging workspace AI tool. It acts as the person using the AI, so it can do only what that person can do in the
/// workspace: it refuses a caller who is not signed in or lacks the tool's permission, and each tool checks the
/// conversation operation on the conversation itself, as the workspace does.
/// </summary>
internal abstract class MessagingAIToolBase : AIFunction
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
    /// Gets the permission the caller needs before the tool runs at all.
    /// </summary>
    protected virtual Permission Permission => MessagingPermissions.UseMessagingWorkspace;

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var user = GetCaller(arguments.Services);

        if (user is null ||
            !await arguments.Services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, Permission))
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
    /// Reads a text argument.
    /// </summary>
    /// <returns>The trimmed text, or <see langword="null"/> when it is missing or blank.</returns>
    protected static string ReadString(AIFunctionArguments arguments, string key)
        => arguments.TryGetFirstString(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    /// <summary>
    /// Reads the <c>page</c> and <c>pageSize</c> arguments, with the page size capped.
    /// </summary>
    protected static (int Page, int PageSize) ReadPaging(AIFunctionArguments arguments)
    {
        var page = Math.Max(1, arguments.GetFirstValueOrDefault("page", 1));
        var pageSize = Math.Clamp(arguments.GetFirstValueOrDefault("pageSize", DefaultPageSize), 1, MaxPageSize);

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
    /// Gets the path of a page of the messaging workspace, or <see langword="null"/> when there is no request to build
    /// it from.
    /// </summary>
    protected static string GetPath(IServiceProvider services, string controller, string action, string id = null)
    {
        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;
        var linkGenerator = services.GetService<LinkGenerator>();

        if (httpContext is null || linkGenerator is null)
        {
            return null;
        }

        var values = new RouteValueDictionary
        {
            ["area"] = typeof(MessagingAIToolBase).Assembly.GetName().Name,
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
