using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.TenantHierarchy.AI;

/// <summary>
/// A tenant hierarchy AI tool. It acts as the person using the AI: it refuses a caller who is not signed in, a tenant
/// that is not the one the matching admin page runs in (a parent tenant, or the Default tenant), and any argument its
/// schema does not declare, so a connection string, a host or a database can never be passed in. Each tool then checks
/// the permission of the admin action it mirrors.
/// </summary>
internal abstract class TenantHierarchyAIToolBase : AIFunction
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

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    private HashSet<string> _argumentNames;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <summary>
    /// Gets the tenant the tool works in.
    /// </summary>
    protected abstract TenantHierarchyToolScope Scope { get; }

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;
        var user = GetCaller(services);

        if (user is null)
        {
            return NotAllowedMessage;
        }

        var shellSettings = services.GetService<ShellSettings>();

        if (Scope == TenantHierarchyToolScope.Parent && shellSettings?.IsParentTenant() != true)
        {
            return "This site is not a parent tenant, so it has no child tenants. The platform administrator makes a tenant a parent from the Default tenant.";
        }

        if (Scope == TenantHierarchyToolScope.Platform && shellSettings?.IsDefaultShell() != true)
        {
            return "The tenant hierarchy is managed from the Default tenant only.";
        }

        _argumentNames ??= GetArgumentNames(JsonSchema);

        if (arguments.Keys.FirstOrDefault(name => !_argumentNames.Contains(name)) is { } unknown)
        {
            return $"Nothing was done: '{unknown}' is not an argument of {Name}. Hosts, URL prefixes, databases, connection strings and other tenant settings are never taken; use only the arguments the tool declares.";
        }

        return await InvokeAsync(arguments, services, user, cancellationToken);
    }

    /// <summary>
    /// Runs the tool for a signed-in caller in the right tenant.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="user">The caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool's result.</returns>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the signed-in caller of the tool: the user the chat hubs publish through <see cref="IUserAccessor"/>, or the
    /// request's user.
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
    /// Checks a permission for the caller.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="user">The caller.</param>
    /// <param name="permission">The permission.</param>
    /// <returns><see langword="true"/> when the caller has the permission.</returns>
    protected static async Task<bool> IsAuthorizedAsync(IServiceProvider services, ClaimsPrincipal user, Permission permission)
    {
        var authorizationService = services.GetService<IAuthorizationService>();

        return authorizationService is not null && await authorizationService.AuthorizeAsync(user, permission);
    }

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
            ? element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => element.ToString(),
            }
            : Convert.ToString(value, CultureInfo.InvariantCulture);

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Returns whether an argument was given, even blank.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns><see langword="true"/> when the argument is present and not null.</returns>
    protected static bool HasArgument(AIFunctionArguments arguments, string name)
        => arguments is not null &&
            arguments.TryGetValue(name, out var value) &&
            value is not null &&
            value is not JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined };

    /// <summary>
    /// Reads a whole number argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The number, or <see langword="null"/> when it is missing or not a whole number.</returns>
    protected static int? ReadInt(AIFunctionArguments arguments, string name)
        => int.TryParse(ReadString(arguments, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>
    /// Reads a yes or no argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The value, or <see langword="null"/> when it is missing.</returns>
    protected static bool? ReadBool(AIFunctionArguments arguments, string name)
        => bool.TryParse(ReadString(arguments, name), out var value) ? value : null;

    /// <summary>
    /// Reads an enum argument by name, in any letter case.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="value">The value read.</param>
    /// <returns><see langword="false"/> when the argument is given but is not a name of the enum.</returns>
    protected static bool TryReadEnum<TEnum>(AIFunctionArguments arguments, string name, out TEnum? value)
        where TEnum : struct, Enum
    {
        value = null;

        var text = ReadString(arguments, name);

        if (text is null)
        {
            return true;
        }

        if (int.TryParse(text, out _) || !Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed))
        {
            return false;
        }

        value = parsed;

        return true;
    }

    /// <summary>
    /// Reads a list of texts, given as an array or as one text separated by commas.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The trimmed, non-empty texts, or <see langword="null"/> when the argument is missing.</returns>
    protected static List<string> ReadStringList(AIFunctionArguments arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        IEnumerable<string> items = value switch
        {
            JsonElement { ValueKind: JsonValueKind.Array } element => element.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString()),
            JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
            JsonElement element => (element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString()).Split(','),
            string text => text.Split(','),
            System.Collections.IEnumerable enumerable => enumerable.Cast<object>().Select(item => Convert.ToString(item, CultureInfo.InvariantCulture)),
            _ => [Convert.ToString(value, CultureInfo.InvariantCulture)],
        };

        return items?
            .Select(item => item?.Trim())
            .Where(item => !string.IsNullOrEmpty(item))
            .ToList();
    }

    /// <summary>
    /// Reads the <c>page</c> and <c>pageSize</c> arguments, with the page size capped.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The page, from 1, and the page size.</returns>
    protected static (int Page, int PageSize) ReadPaging(AIFunctionArguments arguments)
        => (Math.Max(1, ReadInt(arguments, "page") ?? 1), Math.Clamp(ReadInt(arguments, "pageSize") ?? DefaultPageSize, 1, MaxPageSize));

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
    /// Gets the path of a tenant hierarchy admin page.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="routeName">The name of the admin route.</param>
    /// <param name="values">The route values, or <see langword="null"/>.</param>
    /// <returns>The path, or <see langword="null"/> when there is no request to build it from.</returns>
    protected static string GetAdminPath(IServiceProvider services, string routeName, object values = null)
    {
        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;
        var linkGenerator = services.GetService<LinkGenerator>();

        if (httpContext is null || linkGenerator is null)
        {
            return null;
        }

        var routeValues = new RouteValueDictionary(values)
        {
            ["area"] = TenantHierarchyConstants.Features.Area,
        };

        try
        {
            return linkGenerator.GetPathByRouteValues(httpContext, routeName, routeValues);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Finds a child tenant of the current parent, as the child tenant pages do: an entry the platform removed or moved
    /// is not found, unless <paramref name="includeChangedByPlatform"/> is set.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="entryId">The registry entry identifier.</param>
    /// <param name="includeChangedByPlatform">Whether an entry the platform changed is returned.</param>
    /// <returns>The child tenant, or <see langword="null"/>.</returns>
    protected static async Task<ChildTenantInfo> FindChildAsync(IServiceProvider services, string entryId, bool includeChangedByPlatform = false)
    {
        if (string.IsNullOrEmpty(entryId))
        {
            return null;
        }

        var info = await services.GetRequiredService<ITenantHierarchyBroker>().GetChildAsync(entryId);

        return info is null || (!includeChangedByPlatform && info.State == ChildTenantRuntimeState.ChangedByPlatform)
            ? null
            : info;
    }

    /// <summary>
    /// The answer when no child tenant has an entry id.
    /// </summary>
    /// <param name="entryId">The entry id that was asked for.</param>
    /// <returns>The message.</returns>
    protected static string ChildNotFound(string entryId)
        => $"No child tenant has the entry id '{entryId}'. Call listChildTenants to find it.";

    /// <summary>
    /// Describes a child tenant from an explicit list of fields. The database settings and the provisioned resource are
    /// never included.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="info">The child tenant.</param>
    /// <returns>The description.</returns>
    protected static async Task<object> DescribeChildAsync(IServiceProvider services, ChildTenantInfo info)
    {
        var entry = info.Entry;

        return new
        {
            entryId = entry.EntryId,
            displayName = entry.DisplayName,
            slug = entry.Slug,
            address = info.Address,
            state = DescribeState(info),
            runtimeState = info.State.ToString(),
            status = entry.Status.ToString(),
            description = entry.Description,
            recipeName = entry.RecipeName,
            createdAt = await ToLocalAsync(services, entry.CreatedUtc),
            createdBy = entry.CreatedByName,
            removalDate = await ToLocalAsync(services, entry.RetainUntilUtc),
            error = entry.Status == ChildTenantStatus.Failed ? entry.Error : null,
        };
    }

    /// <summary>
    /// Names the state of a child tenant as the list's state filter does.
    /// </summary>
    /// <param name="info">The child tenant.</param>
    /// <returns>The state name.</returns>
    protected static string DescribeState(ChildTenantInfo info)
    {
        if (info.State == ChildTenantRuntimeState.ChangedByPlatform)
        {
            return "ChangedByPlatform";
        }

        return info.Entry.Status switch
        {
            ChildTenantStatus.Failed => "Failed",
            ChildTenantStatus.PendingRemoval or ChildTenantStatus.Removing => "PendingRemoval",
            ChildTenantStatus.Provisioning => "SettingUp",
            _ when info.State == ChildTenantRuntimeState.Initializing => "SettingUp",
            _ when info.State == ChildTenantRuntimeState.Running => "Running",
            _ when info.State == ChildTenantRuntimeState.Suspended => "Suspended",
            _ => info.State.ToString(),
        };
    }

    /// <summary>
    /// Gets the labels the parent's policy uses for its child tenants.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The labels.</returns>
    protected static HierarchyLabels GetLabels(IServiceProvider services)
        => services.GetRequiredService<HierarchyLabelsProvider>().GetLabels();

    private static HashSet<string> GetArgumentNames(JsonElement schema)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        if (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in properties.EnumerateObject())
            {
                names.Add(property.Name);
            }
        }

        return names;
    }
}

/// <summary>
/// The tenant a tenant hierarchy tool works in.
/// </summary>
internal enum TenantHierarchyToolScope
{
    /// <summary>
    /// A parent tenant, for its child tenants.
    /// </summary>
    Parent,

    /// <summary>
    /// The Default tenant, for the whole hierarchy.
    /// </summary>
    Platform,
}
