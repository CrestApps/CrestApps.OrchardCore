using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using OrchardCore.Roles;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.AI.Agent.Roles;

internal sealed class GetRoleTool : AIFunction
{
    public const string TheName = "getRoleInfo";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly RoleManager<IRole> _roleManager;

    public GetRoleTool(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        RoleManager<IRole> roleManager)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _roleManager = roleManager;
        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
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
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Gets role information.";

    public override JsonElement JsonSchema { get; }

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, CommonPermissions.ManageRoles))
        {
            return "The current user does not have permission to manage roles.";
        }

        var roleId = arguments.GetFirstValueOrDefault<string>("roleId");
        var roleName = arguments.GetFirstValueOrDefault<string>("roleName");

        var hasRoleId = !string.IsNullOrEmpty(roleId);
        var hasRoleName = !string.IsNullOrEmpty(roleName);

        if (!hasRoleId && !hasRoleName)
        {
            return "You must provide at least one of the following arguments: roleId, or roleName.";
        }

        IRole role = null;

        if (!hasRoleId)
        {
            role = await _roleManager.FindByIdAsync(roleId);
        }
        else if (!string.IsNullOrEmpty(roleName))
        {
            role = await _roleManager.FindByNameAsync(roleName);
        }

        if (role is null)
        {
            return "Unable to find a role with the provided arguments.";
        }

        if (role is Role r)
        {
            return JsonSerializer.Serialize(r);
        }

        return JsonSerializer.Serialize(role);
    }
}
