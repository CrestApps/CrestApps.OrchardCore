using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.DncRegistry.AI;

/// <summary>
/// A DNC Registry AI tool: it acts as the person using the AI, so it refuses a caller who is not signed in or lacks the
/// permission the matching DNC Registry page checks.
/// </summary>
internal abstract class DncToolBase : AIFunction
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
    /// Gets the permission the caller needs before the tool runs.
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

        var authorizationService = arguments.Services.GetService<IAuthorizationService>();

        if (authorizationService is null || !await authorizationService.AuthorizeAsync(user, Permission))
        {
            return NotAllowedMessage;
        }

        return await InvokeAsync(new Arguments(arguments), arguments.Services, user, cancellationToken);
    }

    /// <summary>
    /// Runs the tool for a signed-in caller who holds the tool's permission.
    /// </summary>
    protected abstract Task<object> InvokeAsync(Arguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the signed-in caller: the chat hubs publish it through <see cref="IUserAccessor"/> for the whole invocation,
    /// and the request's user is the fallback.
    /// </summary>
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
    /// Reads the arguments the model sent.
    /// </summary>
    internal sealed class Arguments
    {
        private readonly JsonObject _values = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="Arguments"/> class.
        /// </summary>
        public Arguments(AIFunctionArguments arguments)
        {
            foreach (var (key, value) in arguments)
            {
                _values[key] = value switch
                {
                    null => null,
                    JsonNode node => node.DeepClone(),
                    JsonElement element => JsonNode.Parse(element.GetRawText()),
                    _ => JsonSerializer.SerializeToNode(value),
                };
            }
        }

        /// <summary>
        /// Gets whether an argument was sent.
        /// </summary>
        public bool Has(string key)
            => _values.ContainsKey(key);

        /// <summary>
        /// Reads a text argument.
        /// </summary>
        /// <returns>The trimmed text, or <see langword="null"/> when it is missing or blank.</returns>
        public string GetString(string key)
            => _values.TryGetPropertyValue(key, out var node) && node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
                ? text.Trim()
                : null;

        /// <summary>
        /// Reads a whole number argument.
        /// </summary>
        /// <returns>The number, or <see langword="null"/> when it is missing or not a whole number.</returns>
        public int? GetInt(string key)
        {
            if (!_values.TryGetPropertyValue(key, out var node) || node is not JsonValue value)
            {
                return null;
            }

            if (value.TryGetValue<int>(out var number))
            {
                return number;
            }

            if (value.TryGetValue<double>(out var real) && real == Math.Floor(real) && real is >= int.MinValue and <= int.MaxValue)
            {
                return (int)real;
            }

            return value.TryGetValue<string>(out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
                ? number
                : null;
        }

        /// <summary>
        /// Reads a list of texts, such as IDs.
        /// </summary>
        /// <returns>The trimmed, distinct, non-blank values; empty when the argument is missing.</returns>
        public List<string> GetStringList(string key)
        {
            if (!_values.TryGetPropertyValue(key, out var node) || node is null)
            {
                return [];
            }

            if (node is JsonValue single && single.TryGetValue<string>(out var one))
            {
                return string.IsNullOrWhiteSpace(one) ? [] : [one.Trim()];
            }

            return node is JsonArray array
                ? array
                    .Select(item => item is JsonValue value && value.TryGetValue<string>(out var text) ? text?.Trim() : null)
                    .Where(text => !string.IsNullOrEmpty(text))
                    .Distinct(StringComparer.Ordinal)
                    .ToList()
                : [];
        }

        /// <summary>
        /// Reads the <c>page</c> and <c>pageSize</c> arguments, with the page size capped.
        /// </summary>
        public (int Page, int PageSize) GetPaging()
            => (Math.Max(1, GetInt("page") ?? 1), Math.Clamp(GetInt("pageSize") ?? DefaultPageSize, 1, MaxPageSize));
    }
}
