using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Agent.Tenants;

public sealed class DisableTenantTool : AIFunction
{
    public const string TheName = "disableTenant";

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

    public override string Description => "Disabled site or tenant.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<DisableTenantTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", Name);
        }

        var shellHost = arguments.Services.GetRequiredService<IShellHost>();
        var shellSettings = arguments.Services.GetRequiredService<ShellSettings>();

        if (!shellSettings.IsDefaultShell())
        {
            logger.LogWarning("AI tool '{ToolName}' failed: not supported outside the default tenant.", Name);

            return "This function is not supported in this tenant. It can only be used in the default tenant.";
        }

        if (!arguments.TryGetFirstString("name", out var name))
        {
            logger.LogWarning("AI tool '{ToolName}' failed: missing 'name' argument.", Name);

            return "Unable to find a name argument in the function arguments.";
        }

        if (!shellHost.TryGetSettings(name, out var tenantSettings))
        {
            logger.LogWarning("AI tool '{ToolName}' failed: tenant '{TenantName}' not found.", Name, name);

            return "The given tenant does not exists.";
        }

        if (tenantSettings.IsDefaultShell())
        {
            logger.LogWarning("AI tool '{ToolName}' failed: cannot disable the default tenant.", Name);

            return "You cannot disable the default tenant.";
        }

        if (!tenantSettings.IsRunning())
        {
            logger.LogWarning("AI tool '{ToolName}' failed: tenant '{TenantName}' is not running.", Name, name);

            return "You can only disable a Running tenant.";
        }

        await shellHost.UpdateShellSettingsAsync(tenantSettings.AsDisabled());

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' completed.", Name);
        }

        return $"The tenant {name} was disabled successfully.";
    }
}
