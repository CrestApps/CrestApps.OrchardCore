using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Removing;

namespace CrestApps.OrchardCore.AI.Agents.Tenants;

public sealed class RemoveTenantTool : AIFunction
{
    public const string TheName = "removeTenant";

    private readonly IShellHost _shellHost;
    private readonly ShellSettings _shellSettings;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShellRemovalManager _shellRemovalManager;

    public RemoveTenantTool(
        IShellHost shellHost,
        ShellSettings shellSettings,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IShellRemovalManager shellRemovalManager)
    {
        _shellHost = shellHost;
        _shellSettings = shellSettings;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _shellRemovalManager = shellRemovalManager;
        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
           """
            {
                "type": "object",
                "properties": {
                    "name": {
                        "type": "string",
                        "description": "A unique name for the tenant to be used as identifier."
                    }
                },
                "additionalProperties": false,
                "required": ["name"]
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Permanently removes a site or a tenant.";

    public override JsonElement JsonSchema { get; }

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, OrchardCorePermissions.ManageTenants))
        {
            return "The current user does not have permission to manage tenants.";
        }

        if (!_shellSettings.IsDefaultShell())
        {
            return "This function is not supported in this tenant. It can only be used in the default tenant.";
        }

        if (!arguments.TryGetFirstString("name", out var name))
        {
            return "Unable to find a name argument in the function arguments.";
        }

        if (!_shellHost.TryGetSettings(name, out var tenantSettings))
        {
            return "The given tenant does not exists.";
        }

        if (tenantSettings.IsDefaultShell())
        {
            return "You cannot enable the default tenant.";
        }

        if (!tenantSettings.IsRemovable())
        {
            return "This tenant cannot be removed.";
        }

        var result = await _shellRemovalManager.RemoveAsync(tenantSettings);

        if (!result.Success)
        {
            return $"The tenant {name} was not removed. ErrorMessage: {result.ErrorMessage}";
        }

        return $"The tenant {name} was removed successfully.";
    }
}
