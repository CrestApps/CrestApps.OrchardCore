using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using OrchardCore.Environment.Shell;
using OrchardCore.Tenants;

namespace CrestApps.OrchardCore.AI.Tools.Tenants;

public sealed class GetTenantOrchardCoreTool : AIFunction
{
    public const string TheName = "getOrchardCoreTenant";

    private readonly IShellHost _shellHost;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public GetTenantOrchardCoreTool(
        IShellHost shellHost,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _shellHost = shellHost;
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

    public override string Description => "Gets info about a tenants in the SaaS environment.";

    public override JsonElement JsonSchema { get; }

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, Permissions.ManageTenants))
        {
            return "The current user does not have permission to manage tenants.";
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

        return JsonSerializer.Serialize(new
        {
            tenantSettings.Name,
            Description = tenantSettings["Description"],
            DatabaseProvider = tenantSettings["DatabaseProvider"],
            RecipeName = tenantSettings["RecipeName"],
            tenantSettings.RequestUrlHost,
            tenantSettings.RequestUrlPrefix,
            Category = tenantSettings["Category"],
            TablePrefix = tenantSettings["TablePrefix"],
            Schema = tenantSettings["Schema"],
            Status = tenantSettings.State.ToString(),
        });
    }
}
