using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Runs the Contact Center dependency health checks and returns each one's verdict, as the dependency probe
/// (<see cref="ContactCenterHealthEndpoints"/>) reports them. Like the probe it never returns exception details, which
/// can carry a connection string.
/// </summary>
internal sealed class GetContactCenterHealthTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getContactCenterHealth";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {},
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Runs the Contact Center dependency health checks (storage, outbox, active calls, queue backlog, dialer pacing, provider ingress, distributed lock, backplane) and returns each status. Use it first when calls or routing misbehave.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ContactCenterPermissions.MonitorContactCenter;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var healthCheckService = services.GetService<HealthCheckService>();

        if (healthCheckService is null)
        {
            return "The health checks are not available: turn on the Health Checks feature to run them.";
        }

        var report = await healthCheckService.CheckHealthAsync(ContactCenterHealthEndpoints.IsDependencyCheck, cancellationToken);

        var checks = report.Entries
            .OrderBy(entry => entry.Value.Status)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMilliseconds = Math.Round(entry.Value.Duration.TotalMilliseconds),
                data = entry.Value.Data
                    .Where(item => item.Value is string or bool or int or long or double or float or decimal)
                    .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
            })
            .ToList();

        return Json(new
        {
            status = report.Status.ToString(),
            checks,
            total = checks.Count,
            unhealthy = checks.Count(check => check.status == nameof(HealthStatus.Unhealthy)),
            degraded = checks.Count(check => check.status == nameof(HealthStatus.Degraded)),
            note = checks.Count == 0
                ? "No Contact Center dependency check is registered on this site."
                : null,
        });
    }
}
