using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.TenantHierarchy.AI;

/// <summary>
/// Suspends a parent tenant with its running children, or resumes it with the children suspended with it, as the
/// Suspend and Resume actions of a parent's page do.
/// </summary>
internal sealed class ChangeParentTenantStateTool : TenantHierarchyAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "changeParentTenantState";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "tenantName": {
          "type": "string",
          "description": "The tenant name of the parent tenant, from getTenantHierarchyOverview."
        },
        "action": {
          "type": "string",
          "enum": ["Suspend", "Resume"],
          "description": "Suspend takes the parent and every running child offline; Resume brings back the parent and exactly the children suspended with it."
        }
      },
      "required": ["tenantName", "action"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Suspends a parent tenant with all its running children, or resumes it with exactly the children suspended with it.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override TenantHierarchyToolScope Scope => TenantHierarchyToolScope.Platform;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, user, TenantHierarchyPermissions.ManageTenantHierarchy))
        {
            return NotAllowedMessage;
        }

        var tenantName = ReadString(arguments, "tenantName");
        var action = ReadString(arguments, "action")?.ToLowerInvariant();
        var platformService = services.GetRequiredService<TenantHierarchyPlatformService>();

        var result = action switch
        {
            "suspend" => await platformService.SuspendParentAsync(tenantName),
            "resume" => await platformService.ResumeParentAsync(tenantName),
            _ => null,
        };

        if (result is null)
        {
            return "Nothing was changed: the action must be Suspend or Resume.";
        }

        if (!result.Succeeded)
        {
            return $"Nothing was changed: {result.Error} Call getTenantHierarchyOverview to find the parent's tenant name.";
        }

        return Json(new
        {
            tenantName,
            suspended = action == "suspend",
            resumed = action == "resume",
            message = action == "suspend"
                ? "The parent tenant and its child tenants were suspended."
                : "The parent tenant and the child tenants suspended with it were resumed.",
        });
    }
}
