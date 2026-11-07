using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.ContactCenter.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// The base of the Contact Center AI tools. Each tool runs as the signed-in user, requires a supervisor-level permission,
/// reads only through the services the supervisor dashboard and the reports read, changes nothing, and keeps what it
/// returns to a size a model can read.
/// </summary>
internal abstract class ContactCenterAIToolBase : AIFunction
{
    /// <summary>
    /// The most characters a tool returns. A larger answer is replaced by a request to narrow the question, so one call can
    /// never flood the model's context or return a whole tenant's records.
    /// </summary>
    internal const int MaxResultCharacters = 24_000;

    /// <summary>
    /// The most rows of one list a tool returns. Every list says how many there were in all.
    /// </summary>
    internal const int MaxListItems = 40;

    /// <summary>
    /// The options the tools write their results with.
    /// </summary>
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <summary>
    /// Gets the permission the user must hold to use the tool. It is the supervisor permission, which an administrator
    /// holds through the manage permission it is implied by.
    /// </summary>
    protected virtual Permission RequiredPermission => ContactCenterPermissions.MonitorContactCenter;

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var services = arguments.Services;
        var user = services?.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return Error("Contact Center tools can only be used by a signed-in person.");
        }

        var authorization = services.GetService<IAuthorizationService>();

        if (authorization is null || !await authorization.AuthorizeAsync(user, RequiredPermission))
        {
            return Error("You don't have the permission to monitor the Contact Center, so this tool can't be used. Ask an administrator for the supervisor permission.");
        }

        var result = await InvokeAsync(arguments, services, user, cancellationToken);
        var json = result as string ?? JsonSerializer.Serialize(result, JsonOptions);

        return json.Length <= MaxResultCharacters
            ? json
            : Error($"The answer was larger than the {MaxResultCharacters} characters a tool may return. Ask again with a narrower filter, such as one queue, one campaign, one agent or a shorter time range.");
    }

    /// <summary>
    /// Runs the tool for a signed-in supervisor.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The services of the request.</param>
    /// <param name="user">The signed-in user.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result to give the model: JSON text, or an object written as JSON.</returns>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken);

    /// <summary>
    /// Writes an error the model can relay and must not work around.
    /// </summary>
    /// <param name="message">What went wrong and what to do about it.</param>
    /// <returns>The error as JSON text.</returns>
    protected static string Error(string message)
        => JsonSerializer.Serialize(new { error = message }, JsonOptions);

    /// <summary>
    /// Reads a text argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The trimmed text, or <see langword="null"/> when it is not given.</returns>
    protected static string Read(AIFunctionArguments arguments, string name)
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
    /// Reads a whole number argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The number, or <see langword="null"/> when it is not given or not a number.</returns>
    protected static int? ReadInt(AIFunctionArguments arguments, string name)
        => int.TryParse(Read(arguments, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>
    /// Reads a UTC date and time argument, as an ISO 8601 text.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The UTC time, or <see langword="null"/> when it is not given or cannot be read.</returns>
    protected static DateTime? ReadUtc(AIFunctionArguments arguments, string name)
        => DateTime.TryParse(
            Read(arguments, name),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var value)
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : null;

    /// <summary>
    /// Makes a JSON schema from text.
    /// </summary>
    /// <param name="schema">The schema.</param>
    /// <returns>The schema.</returns>
    protected static JsonElement Schema(string schema)
        => JsonSerializer.Deserialize<JsonElement>(schema);

    /// <summary>
    /// Writes a UTC time the way the tools do, to the second.
    /// </summary>
    /// <param name="value">The time.</param>
    /// <returns>The time as ISO 8601 text in UTC, or <see langword="null"/>.</returns>
    internal static string Utc(DateTime? value)
        => value?.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    /// Keeps the first rows of a list and says how many there were.
    /// </summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="rows">The rows.</param>
    /// <param name="limit">The most rows to keep.</param>
    /// <returns>The list, with its total and whether it was cut.</returns>
    internal static ToolList<T> Limit<T>(IEnumerable<T> rows, int limit = MaxListItems)
    {
        var all = rows as IReadOnlyList<T> ?? rows.ToList();

        return new ToolList<T>(all.Count, all.Count > limit, all.Take(limit).ToList());
    }
}

/// <summary>
/// A list a tool returns: the rows kept, how many there were in all and whether some were left out.
/// </summary>
/// <typeparam name="T">The row type.</typeparam>
/// <param name="Total">How many rows there were before the list was cut.</param>
/// <param name="Truncated">Whether rows were left out.</param>
/// <param name="Items">The rows kept.</param>
internal sealed record ToolList<T>(int Total, bool Truncated, IReadOnlyList<T> Items);
