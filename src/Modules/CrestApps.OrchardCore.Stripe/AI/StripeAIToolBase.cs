using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.Stripe.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Stripe.AI;

/// <summary>
/// A Stripe AI tool. It acts as the person using the AI: it refuses a caller who is not signed in or who lacks
/// <see cref="StripePermissions.ManageStripeSettings"/>, the permission of Settings › Payments › Stripe, before it reads
/// an argument, and refuses any argument its schema does not declare. No tool returns or takes a key or a secret.
/// </summary>
internal abstract class StripeAIToolBase : AIFunction
{
    /// <summary>
    /// The answer to a caller who is not signed in or lacks the permission.
    /// </summary>
    public const string NotAllowedMessage = "You are not allowed to use this function.";

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

        var services = arguments.Services;
        var user = services.GetService<IUserAccessor>()?.User ?? services.GetService<IHttpContextAccessor>()?.HttpContext?.User;
        var caller = user?.Identity?.IsAuthenticated == true ? user : null;
        var authorizationService = services.GetService<IAuthorizationService>();

        if (caller is null || authorizationService is null || !await authorizationService.AuthorizeAsync(caller, StripePermissions.ManageStripeSettings))
        {
            return NotAllowedMessage;
        }

        var names = JsonSchema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object
            ? properties.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        if (arguments.Keys.FirstOrDefault(name => !names.Contains(name)) is { } unknown)
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
    /// Reads the <c>environment</c> argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="isLive">Whether it names the live environment.</param>
    /// <returns><see langword="false"/> when it is missing or not live or test.</returns>
    protected static bool TryReadEnvironment(AIFunctionArguments arguments, out bool isLive)
    {
        isLive = false;

        if (!arguments.TryGetValue("environment", out var value) || value is null)
        {
            return false;
        }

        var text = value is JsonElement { ValueKind: JsonValueKind.String } element ? element.GetString() : value.ToString();

        if (string.Equals(text, "live", StringComparison.OrdinalIgnoreCase))
        {
            isLive = true;

            return true;
        }

        return string.Equals(text, "test", StringComparison.OrdinalIgnoreCase);
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
}
