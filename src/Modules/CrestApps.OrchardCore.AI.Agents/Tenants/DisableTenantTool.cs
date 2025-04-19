using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Agents.Tenants;

public sealed class DisableTenantTool : AIFunction
{
    public const string TheName = "disableTenant";

    private readonly IShellHost _shellHost;
    private readonly ShellSettings _shellSettings;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public DisableTenantTool(
        IShellHost shellHost,
        ShellSettings shellSettings,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _shellHost = shellHost;
        _shellSettings = shellSettings;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;

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

    public override string Description => "Disabled site or tenant.";

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
            return "You cannot disable the default tenant.";
        }

        if (!tenantSettings.IsRunning())
        {
            return "You can only disable a Running tenant.";
        }

        await _shellHost.UpdateShellSettingsAsync(tenantSettings.AsDisabled());

        return $"The tenant {name} was disabled successfully.";
    }
}
