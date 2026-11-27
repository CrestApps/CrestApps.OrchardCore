using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.ThemeDesigner.Models;
using CrestApps.OrchardCore.ThemeDesigner.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Agent.ThemeDesign;

/// <summary>
/// The base of the theme design tools: they read the active themes and change the signed-in user's theme draft, which
/// only that user sees until it is published.
/// </summary>
public abstract class ThemeDesignTool : AIFunction
{
    /// <summary>
    /// The options the tools write their results with.
    /// </summary>
    protected static readonly JsonSerializerOptions ResultOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc />
    protected sealed override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", Name);
        }

        var result = await InvokeAsync(arguments, arguments.Services, cancellationToken);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' completed.", Name);
        }

        return result;
    }

    /// <summary>
    /// Runs the tool.
    /// </summary>
    /// <param name="arguments">The arguments the model gave.</param>
    /// <param name="services">The request's services.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected abstract ValueTask<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the <c>scope</c> argument: <c>admin</c> for the admin, anything else for the front end.
    /// </summary>
    /// <param name="arguments">The arguments the model gave.</param>
    protected static ThemeDesignScope GetScope(AIFunctionArguments arguments)
        => arguments.TryGetFirstString("scope", out var scope) && string.Equals(scope, "admin", StringComparison.OrdinalIgnoreCase)
            ? ThemeDesignScope.Admin
            : ThemeDesignScope.Site;

    /// <summary>
    /// Writes <paramref name="value"/> as JSON for the model.
    /// </summary>
    /// <param name="value">The value to write.</param>
    protected static string Serialize(object value)
        => JsonSerializer.Serialize(value, ResultOptions);

    /// <summary>
    /// Writes the outcome of a change to the draft for the model.
    /// </summary>
    /// <param name="result">The outcome.</param>
    /// <param name="success">What to say when the change was made.</param>
    protected static string Report(ThemeDesignResult result, string success)
        => result.Succeeded
            ? success
            : "The change was not made: " + string.Join(" ", result.Errors);

    /// <summary>
    /// Gets whether the signed-in user has <paramref name="permission"/>.
    /// </summary>
    /// <param name="services">The request's services.</param>
    /// <param name="permission">The permission.</param>
    protected static async Task<bool> CanAsync(IServiceProvider services, Permission permission)
    {
        var user = services.GetRequiredService<IHttpContextAccessor>().HttpContext?.User;

        return user is not null && await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, permission);
    }

    /// <summary>
    /// Gets the permission a side of the site's templates need.
    /// </summary>
    /// <param name="scope">The side of the site.</param>
    protected static Permission GetTemplatesPermission(ThemeDesignScope scope)
        => scope == ThemeDesignScope.Admin
            ? CrestApps.OrchardCore.ThemeDesigner.ThemeDesignerPermissions.ManageAdminTemplates
            : CrestApps.OrchardCore.ThemeDesigner.ThemeDesignerPermissions.ManageTemplates;

    /// <summary>
    /// Gets the draft service.
    /// </summary>
    /// <param name="services">The request's services.</param>
    protected static IThemeDesignDraftService GetDraftService(IServiceProvider services)
        => services.GetRequiredService<IThemeDesignDraftService>();
}
