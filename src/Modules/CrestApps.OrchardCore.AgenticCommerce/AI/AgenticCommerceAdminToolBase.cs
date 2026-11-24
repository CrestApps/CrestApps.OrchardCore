using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AgenticCommerce.AI;

/// <summary>
/// An agentic commerce admin AI tool. It acts as the person using the AI: through <see cref="AIModuleTools"/> it refuses a
/// caller who is not signed in or who lacks the tool's <see cref="Permission"/>, the permission of the admin page it
/// mirrors, before it reads an argument, and it refuses any argument its schema does not declare. Unlike the storefront
/// tools of this module, these tools are never safe for anonymous visitors.
/// </summary>
internal abstract class AgenticCommerceAdminToolBase : AIFunction
{
    /// <summary>
    /// The answer to a caller who is not signed in or lacks the permission.
    /// </summary>
    public const string NotAllowedMessage = "You are not allowed to use this function.";

    private HashSet<string> _argumentNames;

    /// <summary>
    /// Gets the permission the caller needs: the one the admin page this tool mirrors checks.
    /// </summary>
    protected abstract Permission Permission { get; }

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
        var caller = AIModuleTools.GetCaller(services);

        if (caller is null || !await AIModuleTools.IsAuthorizedAsync(services, Permission))
        {
            return NotAllowedMessage;
        }

        _argumentNames ??= GetArgumentNames(JsonSchema);

        if (arguments.Keys.FirstOrDefault(name => !_argumentNames.Contains(name)) is { } unknown)
        {
            return $"Nothing was done: '{unknown}' is not an argument of {Name}. Use only the arguments the tool declares.";
        }

        return await InvokeAsync(arguments, services, caller, cancellationToken);
    }

    /// <summary>
    /// Runs the tool for a signed-in caller who has the permission.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool's result.</returns>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a UTC time in the caller's or the site's time zone.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="utc">The UTC time, or <see langword="null"/>.</param>
    /// <returns>The local time with its offset, or <see langword="null"/>.</returns>
    protected static Task<string> LocalTimeAsync(IServiceProvider services, DateTime? utc)
        => AIModuleTools.ToLocalAsync(services.GetService<ILocalClock>(), utc);

    /// <summary>
    /// Writes an enum value as the tools name it, starting in lower case.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The name, such as <c>requiresEscalation</c>.</returns>
    protected static string ToCamel(string value)
        => string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];

    /// <summary>
    /// Builds a list result: the page's items, the total, the paging and whether more entries exist.
    /// </summary>
    /// <param name="items">The page's items, each built from an explicit list of fields.</param>
    /// <param name="total">How many entries match.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="note">A note for the model, or <see langword="null"/>.</param>
    /// <returns>The JSON text.</returns>
    protected static string ListResult(IReadOnlyCollection<object> items, int total, int page, int pageSize, string note = null)
        => AIModuleTools.Json(new
        {
            items,
            total,
            page,
            pageSize,
            truncated = ((page - 1) * pageSize) + items.Count < total,
            note,
        });

    /// <summary>
    /// Parses a JSON schema.
    /// </summary>
    /// <param name="schema">The schema text.</param>
    /// <returns>The schema.</returns>
    protected static JsonElement Schema(string schema)
        => JsonSerializer.Deserialize<JsonElement>(schema);

    /// <summary>
    /// Reads an argument as JSON.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The value, or <see langword="null"/> when it is missing or null.</returns>
    protected static JsonElement? ReadJson(AIFunctionArguments arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var element = value is JsonElement json ? json : JsonSerializer.SerializeToElement(value);

        return element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : element;
    }

    /// <summary>
    /// Reads a text argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The trimmed text, or <see langword="null"/> when it is missing or blank.</returns>
    protected static string ReadString(AIFunctionArguments arguments, string name)
    {
        if (ReadJson(arguments, name) is not { } element)
        {
            return null;
        }

        var text = element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Reads a yes or no argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The value, or <see langword="null"/> when it is missing or not true or false.</returns>
    protected static bool? ReadBool(AIFunctionArguments arguments, string name)
        => ReadJson(arguments, name) is { } element
            ? element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(element.GetString(), out var parsed) => parsed,
                _ => null,
            }
            : null;

    /// <summary>
    /// Reads the page and page size arguments, with the page size capped.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="pageSize">The page size, from 1 to <see cref="AIModuleTools.MaxPageSize"/>.</param>
    /// <returns><see langword="false"/> when either is given but is not a whole number.</returns>
    protected static bool TryReadPaging(AIFunctionArguments arguments, out int page, out int pageSize)
    {
        page = 1;
        pageSize = AIModuleTools.DefaultPageSize;

        if (!TryReadInt(arguments, "page", out var askedPage) || !TryReadInt(arguments, "pageSize", out var askedSize))
        {
            return false;
        }

        page = Math.Max(1, askedPage ?? 1);
        pageSize = Math.Clamp(askedSize ?? AIModuleTools.DefaultPageSize, 1, AIModuleTools.MaxPageSize);

        return true;
    }

    private static bool TryReadInt(AIFunctionArguments arguments, string name, out int? value)
    {
        value = null;

        if (ReadJson(arguments, name) is not { } element)
        {
            return true;
        }

        if ((element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number)) ||
            (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)))
        {
            value = number;

            return true;
        }

        return false;
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
