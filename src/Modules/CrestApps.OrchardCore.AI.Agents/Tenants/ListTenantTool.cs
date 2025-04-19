using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Agents.Tenants;

public sealed class ListTenantTool : AIFunction
{
    public const string TheName = "listTenant";

    private readonly IShellHost _shellHost;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public ListTenantTool(
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

    public override string Description => "Lists all sites or tenants.";

    public override JsonElement JsonSchema { get; }

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, OrchardCorePermissions.ManageTenants))
        {
            return "The current user does not have permission to manage tenants.";
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
