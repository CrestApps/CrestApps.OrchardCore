using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using OrchardCore.Environment.Shell;
using OrchardCore.Tenants;

namespace CrestApps.OrchardCore.AI.Tools.Tenants;

public sealed class ListTenantOrchardCoreTool : AIFunction
{
    public const string TheName = "listAlOrchardCoreTenant";

    private readonly IShellHost _shellHost;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public ListTenantOrchardCoreTool(
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
                "additionalProperties": false,
                "required": []
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Lists all tenants in the SaaS environment.";

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

        var shells = _shellHost.GetAllSettings();

        return JsonSerializer.Serialize(shells.Select(s => new
        {
            s.Name,
            Description = s["Description"],
            DatabaseProvider = s["DatabaseProvider"],
            RecipeName = s["RecipeName"],
            s.RequestUrlHost,
            s.RequestUrlPrefix,
            Category = s["Category"],
            TablePrefix = s["TablePrefix"],
            Schema = s["Schema"],
            Status = s.State.ToString(),
        }));
    }
}
