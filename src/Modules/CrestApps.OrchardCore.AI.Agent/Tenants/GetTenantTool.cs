using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Agent.Tenants;

public sealed class GetTenantTool: AIFunction
{
    public const string TheName = "getTenant";

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

    public override string Description => "Gets info about a site or a tenant.";

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
        if (!await arguments.IsAuthorizedAsync(OrchardCorePermissions.ManageTenants))
        {
            return "The current user does not have permission to manage tenants.";
        }

        if (!arguments.TryGetFirstString("name", out var name))
        {
            return "Unable to find a name argument in the function arguments.";
        }

        if (!shellHost.TryGetSettings(name, out var tenantSettings))
        {
            return "The given tenant does not exists.";
        }

        return JsonSerializer.Serialize(tenantSettings.AsAIObject());
    }
}
