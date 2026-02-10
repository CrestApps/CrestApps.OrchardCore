using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Agent.Tenants;

public sealed class ListTenantTool: AIFunction
{
    public const string TheName = "listTenant";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {},
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description => "Lists all sites or tenants.";

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

        var shells = shellHost.GetAllSettings();

        return JsonSerializer.Serialize(shells.Select(x => x.AsAIObject()));
    }
}
