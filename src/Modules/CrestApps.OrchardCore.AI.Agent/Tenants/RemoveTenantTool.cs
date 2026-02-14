using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Removing;

namespace CrestApps.OrchardCore.AI.Agent.Tenants;

public sealed class RemoveTenantTool: AIFunction
{
    public const string TheName = "removeTenant";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
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
        """);

    public override string Name => TheName;

    public override string Description => "Permanently removes a site or a tenant.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var shellHost = arguments.Services.GetRequiredService<IShellHost>();
        var shellSettings = arguments.Services.GetRequiredService<ShellSettings>();
        var shellRemovalManager = arguments.Services.GetRequiredService<IShellRemovalManager>();

        if (!await arguments.IsAuthorizedAsync(OrchardCorePermissions.ManageTenants))
        {
            return "The current user does not have permission to manage tenants.";
        }

        if (!shellSettings.IsDefaultShell())
        {
            return "This function is not supported in this tenant. It can only be used in the default tenant.";
        }

        if (!arguments.TryGetFirstString("name", out var name))
        {
            return "Unable to find a name argument in the function arguments.";
        }

        if (!shellHost.TryGetSettings(name, out var tenantSettings))
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

        var result = await shellRemovalManager.RemoveAsync(tenantSettings);

        if (!result.Success)
        {
            return $"The tenant {name} was not removed. ErrorMessage: {result.ErrorMessage}";
        }

        return $"The tenant {name} was removed successfully.";
    }
}
