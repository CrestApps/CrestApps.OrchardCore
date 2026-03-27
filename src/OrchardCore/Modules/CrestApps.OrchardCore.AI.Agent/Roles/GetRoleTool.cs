using System.Text.Json;
using CrestApps.AI.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.AI.Agent.Roles;

internal sealed class GetRoleTool : AIFunction
{
    public const string TheName = "getRoleInfo";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
       """
        {
          "type": "object",
          "properties": {
            "roleId": {
              "type": "string",
              "description": "The roleId to get role info for."
            },
            "roleName": {
              "type": "string",
              "description": "The roleName to get role info for."
            }
          },
          "additionalProperties": false,
          "required": []
        }
        """);

    public override string Name => TheName;

    public override string Description => "Gets role information.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<GetRoleTool>>();
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", Name);
        }

        var roleManager = arguments.Services.GetRequiredService<RoleManager<IRole>>();

        var roleId = arguments.GetFirstValueOrDefault<string>("roleId");
        var roleName = arguments.GetFirstValueOrDefault<string>("roleName");

        var hasRoleId = !string.IsNullOrEmpty(roleId);
        var hasRoleName = !string.IsNullOrEmpty(roleName);

        if (!hasRoleId && !hasRoleName)
        {
            logger.LogWarning("AI tool '{ToolName}': neither 'roleId' nor 'roleName' argument was provided.", Name);

            return "You must provide at least one of the following arguments: roleId, or roleName.";
        }

        IRole role = null;

        if (hasRoleId)
        {
            role = await roleManager.FindByIdAsync(roleId);
        }
        else if (hasRoleName)
        {
            role = await roleManager.FindByNameAsync(roleName);
        }

        if (role is null)
        {
            logger.LogWarning("AI tool '{ToolName}': no role found for roleId '{RoleId}' or roleName '{RoleName}'.", Name, roleId, roleName);

            return "Unable to find a role with the provided arguments.";
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' completed.", Name);
        }

        if (role is Role r)
        {
            return JsonSerializer.Serialize(r);
        }

        return JsonSerializer.Serialize(role);
    }
}
