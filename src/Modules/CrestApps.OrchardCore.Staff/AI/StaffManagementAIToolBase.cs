using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Staff.AI;

/// <summary>
/// A staff management AI tool. It acts as the person using the AI: it refuses a caller who is not signed in, or whom the
/// tool's gate refuses, before it reads an argument, and refuses any argument its schema does not declare. Unlike the
/// older <see cref="StaffAIToolBase"/>, the gate is the tool's own, so the people a manager manages (their staff scope)
/// can be reached without the <see cref="StaffPermissions.ViewStaff"/> permission, as on the staff pages.
/// </summary>
internal abstract class StaffManagementAIToolBase : AIFunction
{
    /// <summary>
    /// The answer to a caller who is not signed in or whom the gate refuses.
    /// </summary>
    public const string NotAllowedMessage = "You are not allowed to use this function.";

    /// <summary>
    /// The default number of entries a list returns.
    /// </summary>
    protected const int DefaultPageSize = 20;

    /// <summary>
    /// The most entries a list returns.
    /// </summary>
    protected const int MaxPageSize = 50;

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private HashSet<string> _argumentNames;

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

        var services = arguments.Services;
        var user = services.GetService<IUserAccessor>()?.User
            ?? services.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true ||
            services.GetService<IAuthorizationService>() is null ||
            !await IsAllowedAsync(services, user, cancellationToken))
        {
            return NotAllowedMessage;
        }

        _argumentNames ??= GetArgumentNames(JsonSchema);

        if (arguments.Keys.FirstOrDefault(name => !_argumentNames.Contains(name)) is { } unknown)
        {
            return $"Nothing was done: '{unknown}' is not an argument of {Name}. Use only the arguments the tool declares.";
        }

        return await InvokeAsync(arguments, services, user, cancellationToken);
    }

    /// <summary>
    /// Gets whether a signed-in caller may use the tool at all, the gate the matching staff page applies first.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the caller may use the tool.</returns>
    protected abstract Task<bool> IsAllowedAsync(IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken);

    /// <summary>
    /// Runs the tool for a caller the gate allows.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool's result.</returns>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken);

    /// <summary>
    /// Checks a permission for the caller.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="permission">The permission.</param>
    /// <returns><see langword="true"/> when granted.</returns>
    protected static async Task<bool> HasAsync(IServiceProvider services, ClaimsPrincipal caller, Permission permission)
        => await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(caller, permission);

    /// <summary>
    /// Gets whether the caller manages somebody, the gate of the staff editor (everybody with
    /// <see cref="StaffPermissions.ManageStaff"/>; their reports and teams with <see cref="StaffPermissions.ManageOwnStaff"/>).
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the caller's staff scope is not empty.</returns>
    protected static async Task<bool> ManagesAnybodyAsync(IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
        => !(await services.GetRequiredService<IStaffScopeService>().GetScopeAsync(caller, cancellationToken)).IsEmpty;

    /// <summary>
    /// Finds the staff member the arguments name: <c>staffMemberId</c>, else <c>employeeNumber</c>, else <c>name</c> (an
    /// exact name wins; several people who match are listed so the caller can choose).
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The services.</param>
    /// <param name="canSee">Whether the caller may see a staff member. People the caller may not see are never named or
    /// listed, and the answer is the same whether such a person exists or not.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The staff member, or the sentence that explains why none was found.</returns>
    protected static async Task<(StaffMember Staff, string Error)> FindStaffAsync(AIFunctionArguments arguments, IServiceProvider services, Func<StaffMember, Task<bool>> canSee, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(canSee);

        var manager = services.GetRequiredService<IStaffMemberManager>();

        if (ReadString(arguments, "staffMemberId") is { } id)
        {
            return await manager.FindByIdAsync(id, cancellationToken) is { } byId && await canSee(byId)
                ? (byId, null)
                : (null, $"No staff member you may see has the id '{id}'. Use getStaffMember with a name to find them.");
        }

        if (ReadString(arguments, "employeeNumber") is { } number)
        {
            return await manager.FindByEmployeeNumberAsync(number, cancellationToken) is { } byNumber && await canSee(byNumber)
                ? (byNumber, null)
                : (null, $"No staff member you may see has the employee number '{number}'.");
        }

        if (ReadString(arguments, "name") is not { } name)
        {
            return (null, "Give the staffMemberId, the employeeNumber or the name of the staff member.");
        }

        var (_, found) = await manager.SearchAsync(new StaffQuery { Search = name }, 0, MaxPageSize, cancellationToken);
        var matches = new List<StaffMember>();

        foreach (var staff in found)
        {
            if (await canSee(staff))
            {
                matches.Add(staff);
            }
        }

        var exact = matches.Where(staff => string.Equals(staff.DisplayName, name, StringComparison.OrdinalIgnoreCase)).ToArray();

        if (exact.Length == 1)
        {
            return (exact[0], null);
        }

        if (matches.Count == 1)
        {
            return (matches[0], null);
        }

        if (matches.Count == 0)
        {
            return (null, $"No staff member you may see matches '{name}'.");
        }

        var candidates = (exact.Length > 1 ? exact : matches.Take(10)).Select(staff => $"{staff.DisplayName} ({staff.JobTitle ?? "no job title"}, id {staff.ItemId})");

        return (null, $"Several staff members match '{name}': {string.Join("; ", candidates)}. Give the staffMemberId of the one you mean.");
    }

    /// <summary>
    /// Gets the path of a staff member's editor.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="staffMemberId">The staff member.</param>
    /// <returns>The path, or <see langword="null"/> when there is no request to build it from.</returns>
    protected static string GetEditorPath(IServiceProvider services, string staffMemberId)
    {
        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;
        var linkGenerator = services.GetService<LinkGenerator>();

        if (httpContext is null || linkGenerator is null)
        {
            return null;
        }

        try
        {
            return linkGenerator.GetPathByAction(httpContext, "Edit", "Staff", new { area = StaffConstants.Features.Staff, id = staffMemberId });
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes a UTC time in the caller's or the site's time zone.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="utc">The UTC time.</param>
    /// <returns>The local time as text, or <see langword="null"/>.</returns>
    protected static async Task<string> LocalAsync(IServiceProvider services, DateTime? utc)
    {
        if (utc is not { } value)
        {
            return null;
        }

        var offset = new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));

        if (services.GetService<ILocalClock>() is { } localClock)
        {
            offset = await localClock.ConvertToLocalAsync(offset);
        }

        return offset.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Writes a date as yyyy-MM-dd.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>The text, or <see langword="null"/>.</returns>
    protected static string Date(DateOnly? date)
        => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

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
    /// Gets whether an argument was given, even empty.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns><see langword="true"/> when the argument was given.</returns>
    protected static bool Has(AIFunctionArguments arguments, string name)
        => arguments is not null && arguments.ContainsKey(name);

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
    /// Reads a yes or no argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The value, or <see langword="null"/> when it is missing.</returns>
    protected static bool? ReadBool(AIFunctionArguments arguments, string name)
        => bool.TryParse(ReadString(arguments, name), out var value) ? value : null;

    /// <summary>
    /// Reads a whole number argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The number, or <see langword="null"/> when it is missing or not a whole number.</returns>
    protected static int? ReadInt(AIFunctionArguments arguments, string name)
        => int.TryParse(ReadString(arguments, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>
    /// Reads a date written as yyyy-MM-dd.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="date">The date, or <see langword="null"/> when the text is empty.</param>
    /// <returns><see langword="false"/> when the text is not a date.</returns>
    protected static bool TryParseDate(string text, out DateOnly? date)
    {
        date = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
        {
            date = value;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Reads an argument as a JSON element, whatever form the caller passed it in.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The element, or <see langword="null"/> when it is missing.</returns>
    protected static JsonElement? ReadElement(AIFunctionArguments arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        return value is JsonElement element ? element : JsonSerializer.SerializeToElement(value, _jsonOptions);
    }

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
