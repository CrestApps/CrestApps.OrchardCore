using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using OrchardCore.Environment.Shell;
using OrchardCore.Tenants;

namespace CrestApps.OrchardCore.AI.Tools.Tenants;

public sealed class DisableTenantOrchardCoreTool : AIFunction
{
    public const string TheName = "disableOrchardCoreTenant";

    private readonly IShellHost _shellHost;
    private readonly ShellSettings _shellSettings;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public DisableTenantOrchardCoreTool(
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

    public override string Description => "Disabled site in a SaaS environment.";

    public override JsonElement JsonSchema { get; }

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, Permissions.ManageTenants))
        {
            return "The current user does not have permission to manage tenants.";
        }

        if (!_shellSettings.IsDefaultShell())
        {
            return "This function is not supported in this tenant. It can only be used in the default tenant.";
        }

        if (!arguments.TryGetValue("name", out var nameArg))
        {
            return "Unable to find a name argument in the function arguments.";
        }

        var name = ToolHelpers.GetStringValue(nameArg);

        if (string.IsNullOrEmpty(name))
        {
            return "The name argument is required.";
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
