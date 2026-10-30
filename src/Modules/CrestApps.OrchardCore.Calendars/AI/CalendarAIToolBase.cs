using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Calendars.AI;

/// <summary>
/// A calendar AI tool. It acts as the person using the AI, so it can do only what that person can do in the calendar.
/// </summary>
public abstract class CalendarAIToolBase : AIFunction
{
    /// <summary>
    /// The options the tools write their results with.
    /// </summary>
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var user = arguments.Services?.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return "The calendar can only be used by a signed-in person.";
        }

        return await InvokeAsync(arguments, arguments.Services, user, cancellationToken);
    }

    /// <summary>
    /// Runs the tool for a signed-in person.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The request's services.</param>
    /// <param name="user">The person using the AI.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The tool's result.</returns>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a string argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
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
    /// Reads a list argument, given as an array or a comma separated string.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The items.</returns>
    protected static IList<string> ReadList(AIFunctionArguments arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is null)
        {
            return [];
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Array } array)
        {
            return array.EnumerateArray().Select(item => item.ToString()).Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
        }

        return Workflows.CalendarWorkflowService.Split(Read(arguments, name));
    }

    /// <summary>
    /// Parses a JSON schema.
    /// </summary>
    /// <param name="schema">The schema text.</param>
    /// <returns>The schema.</returns>
    protected static JsonElement Schema(string schema)
    {
        return JsonSerializer.Deserialize<JsonElement>(schema);
    }
}
