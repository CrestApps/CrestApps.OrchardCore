using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Surveys.Core.Access;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Surveys.AI.Tools;

/// <summary>
/// The base of the Surveys AI tools: they work only for a signed-in person, and each checks that person's access to
/// every survey it reads or writes.
/// </summary>
public abstract class SurveysAIToolBase : AIFunction
{
    /// <summary>
    /// The options results are serialized with.
    /// </summary>
    protected static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var services = arguments.Services;
        var user = services?.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(SurveyUserIds.Of(user)))
        {
            return "This tool works only for a signed-in person.";
        }

        return await InvokeAsync(arguments, services, user, cancellationToken);
    }

    /// <summary>
    /// Runs the tool for a signed-in person.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The services.</param>
    /// <param name="user">The person.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a text argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The name.</param>
    /// <returns>The text, or <see langword="null"/>.</returns>
    protected static string Read(AIFunctionArguments arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var text = value is JsonElement element
            ? element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString()
            : Convert.ToString(value, CultureInfo.InvariantCulture);

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Parses a JSON schema.
    /// </summary>
    /// <param name="schema">The schema.</param>
    /// <returns>The schema element.</returns>
    protected static JsonElement Schema(string schema)
        => JsonSerializer.Deserialize<JsonElement>(schema);

    /// <summary>
    /// Serializes a result.
    /// </summary>
    /// <param name="value">The result.</param>
    /// <returns>The JSON.</returns>
    protected static string Json(object value)
        => JsonSerializer.Serialize(value, JsonOptions);
}
