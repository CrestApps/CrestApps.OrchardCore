using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.AI.Agent.HealthChecks;

/// <summary>
/// Runs the health checks the enabled features registered, through the same health check service the Health Checks
/// endpoint uses, and returns what its default response writer shows: the overall status and each check's name,
/// status and description. Exceptions and check data are left out, as they can carry connection details.
/// </summary>
public sealed class GetSiteHealthTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getSiteHealth";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "checkName": {
          "type": "string",
          "description": "The name of one health check to run, such as 'Redis'. Leave it out to run every check."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Runs the site's registered health checks and returns the overall status and each check's status and description. Use it when the user asks whether the site or one of its dependencies is working.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        // The health endpoint is public; the tool reports on the site's dependencies, so it asks for admin access.
        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, AdminPermissions.AccessAdminPanel))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var healthCheckService = services.GetService<HealthCheckService>();

        if (healthCheckService is null)
        {
            return "The health checks are not available: the Health Checks feature does not register its service on this site.";
        }

        arguments.TryGetFirstString("checkName", out var checkName);
        checkName = checkName?.Trim();

        var report = string.IsNullOrEmpty(checkName)
            ? await healthCheckService.CheckHealthAsync(cancellationToken)
            : await healthCheckService.CheckHealthAsync(registration => string.Equals(registration.Name, checkName, StringComparison.OrdinalIgnoreCase), cancellationToken);

        if (!string.IsNullOrEmpty(checkName) && report.Entries.Count == 0)
        {
            return $"There is no health check named '{checkName}'. Leave the name out to run every check the site registers.";
        }

        return AgentToolResults.Json(new
        {
            status = report.Status.ToString(),
            durationMilliseconds = Math.Round(report.TotalDuration.TotalMilliseconds),
            checks = report.Entries
                .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .Select(entry => new
                {
                    name = entry.Key,
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    durationMilliseconds = Math.Round(entry.Value.Duration.TotalMilliseconds),
                })
                .ToList(),
            note = report.Entries.Count == 0
                ? "No feature registered a health check, so the status only says the site answered."
                : null,
        });
    }
}
