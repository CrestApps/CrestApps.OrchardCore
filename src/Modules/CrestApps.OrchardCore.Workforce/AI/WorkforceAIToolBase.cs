using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// The base of the Workforce AI tools: they run as the signed-in user, with the same permission and team scope checks
/// as the admin and the My Work portal.
/// </summary>
public abstract class WorkforceAIToolBase : AIFunction
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
            return "Workforce tools can only be used by a signed-in person.";
        }

        return await InvokeAsync(arguments, arguments.Services, user, cancellationToken);
    }

    /// <summary>
    /// Runs the tool.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The services.</param>
    /// <param name="user">The signed-in user.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result to give the model.</returns>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the worker linked to the user, or <see langword="null"/>.
    /// </summary>
    protected static Task<WorkerProfile> WorkerOfAsync(IServiceProvider services, ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        return string.IsNullOrEmpty(userId) ? Task.FromResult<WorkerProfile>(null) : services.GetRequiredService<IWorkerManager>().FindByUserIdAsync(userId);
    }

    /// <summary>
    /// Reads a text argument.
    /// </summary>
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
    protected static int? ReadInt(AIFunctionArguments arguments, string name)
    {
        return int.TryParse(Read(arguments, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    /// <summary>
    /// Reads a day argument (yyyy-MM-dd).
    /// </summary>
    protected static DateOnly? ReadDate(AIFunctionArguments arguments, string name)
    {
        var text = Read(arguments, name);

        return DateOnly.TryParse(text?.Length > 10 ? text[..10] : text, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    /// <summary>
    /// Reads a local date and time argument (yyyy-MM-ddTHH:mm).
    /// </summary>
    protected static DateTime? ReadDateTime(AIFunctionArguments arguments, string name)
    {
        return DateTime.TryParse(Read(arguments, name), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? DateTime.SpecifyKind(value, DateTimeKind.Unspecified) : null;
    }

    /// <summary>
    /// Makes a JSON schema from text.
    /// </summary>
    protected static JsonElement Schema(string schema)
    {
        return JsonSerializer.Deserialize<JsonElement>(schema);
    }

    /// <summary>
    /// Writes a local time for the model.
    /// </summary>
    protected static string Local(DateTime value)
    {
        return value.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
    }
}
