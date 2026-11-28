using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.ContentTransfer.Indexes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using YesSql;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContentTransfer.AI;

/// <summary>
/// A content transfer AI tool. It acts as the person using the AI: it refuses a caller who is not signed in, and each
/// tool checks the permission the matching admin action asks for, per content type where the action does.
/// </summary>
public abstract class ContentTransferAIToolBase : AIFunction
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

    // The result is read by the model, not put in a page, so names in any language stay as written instead of becoming
    // \uXXXX escapes.
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

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

        return await InvokeAsync(arguments, arguments.Services, user, cancellationToken);
    }

    /// <summary>
    /// Runs the tool for a signed-in caller. The tool checks its own permission.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="user">The caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool's result.</returns>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the signed-in caller of the tool. The chat hubs publish the caller through <see cref="IUserAccessor"/> for the
    /// whole invocation, which also works on transports where the request is not available to the hub method; the
    /// request's user is the fallback.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The caller, or <see langword="null"/> when nobody is signed in.</returns>
    protected static ClaimsPrincipal GetCaller(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var user = services.GetService<IUserAccessor>()?.User
            ?? services.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        return user?.Identity?.IsAuthenticated == true
            ? user
            : null;
    }

    /// <summary>
    /// Gets the user ID of the caller, which the export entries record as their owner.
    /// </summary>
    /// <param name="user">The caller.</param>
    /// <returns>The user ID.</returns>
    protected static string GetUserId(ClaimsPrincipal user)
        => user?.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    /// Whether the caller holds a permission, on a content type when one is given, as the admin actions check it.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="user">The caller.</param>
    /// <param name="permission">The permission.</param>
    /// <param name="contentType">The content type the permission is checked on, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the caller holds it.</returns>
    protected static async Task<bool> IsAuthorizedAsync(IServiceProvider services, ClaimsPrincipal user, Permission permission, string contentType = null)
    {
        var authorizationService = services.GetService<IAuthorizationService>();

        return user is not null && authorizationService is not null && (contentType is null
            ? await authorizationService.AuthorizeAsync(user, permission)
            : await authorizationService.AuthorizeAsync(user, permission, (object)contentType));
    }

    /// <summary>
    /// Finds a content transfer entry by its ID.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="entryId">The entry's ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entry, or <see langword="null"/>.</returns>
    protected static Task<ContentTransferEntry> FindEntryAsync(IServiceProvider services, string entryId, CancellationToken cancellationToken)
        => services.GetRequiredService<YesSqlSession>()
            .Query<ContentTransferEntry, ContentTransferEntryIndex>(index => index.EntryId == entryId)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Writes a result as JSON.
    /// </summary>
    /// <param name="value">The result, built from an explicit list of fields.</param>
    /// <returns>The JSON text.</returns>
    protected static string Json(object value)
        => JsonSerializer.Serialize(value, _jsonOptions);

    /// <summary>
    /// Parses a JSON schema.
    /// </summary>
    /// <param name="schema">The schema text.</param>
    /// <returns>The schema.</returns>
    protected static JsonElement Schema(string schema)
        => JsonSerializer.Deserialize<JsonElement>(schema);

    /// <summary>
    /// Reads a text argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The trimmed text, or <see langword="null"/> when it is missing or blank.</returns>
    protected static string ReadString(AIFunctionArguments arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var text = value is JsonElement element
            ? element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString()
            : value.ToString();

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Reads the <c>page</c> and <c>pageSize</c> arguments, with the page size capped.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The page, from 1, and the page size.</returns>
    protected static (int Page, int PageSize) ReadPaging(AIFunctionArguments arguments)
    {
        var page = int.TryParse(ReadString(arguments, "page"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPage) ? parsedPage : 1;
        var pageSize = int.TryParse(ReadString(arguments, "pageSize"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSize) ? parsedSize : DefaultPageSize;

        return (Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));
    }

    /// <summary>
    /// Converts a stored UTC time to the user's or the site's time zone.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="utc">The UTC time.</param>
    /// <returns>The local time with its offset, or <see langword="null"/>.</returns>
    protected static async Task<string> ToLocalAsync(IServiceProvider services, DateTime? utc)
    {
        var localClock = services.GetService<ILocalClock>();

        if (utc is null || utc.Value == default || localClock is null)
        {
            return null;
        }

        var local = await localClock.ConvertToLocalAsync(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc));

        return local.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets the path of a Content Transfer admin page or download.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="action">The action of the module's admin controller.</param>
    /// <param name="values">Other route values, such as the entry's ID.</param>
    /// <returns>The path, or <see langword="null"/> when there is no request to build it from.</returns>
    protected static string GetAdminPath(IServiceProvider services, string action, RouteValueDictionary values = null)
    {
        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;
        var linkGenerator = services.GetService<LinkGenerator>();

        if (httpContext is null || linkGenerator is null)
        {
            return null;
        }

        var routeValues = new RouteValueDictionary(values)
        {
            ["area"] = ContentTransferConstants.Feature.ModuleId,
            ["controller"] = "Admin",
            ["action"] = action,
        };

        return linkGenerator.GetPathByRouteValues(httpContext, null, routeValues);
    }
}
