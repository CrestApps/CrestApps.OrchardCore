using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using OrchardCore.Data;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Agent.Tenants;

public sealed class CreateTenantTool : AIFunction
{
    public const string TheName = "createTenant";

    private readonly IShellHost _shellHost;
    private readonly ShellSettings _shellSettings;
    private readonly IShellSettingsManager _shellSettingsManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<DatabaseProvider> _databaseProviders;

    public CreateTenantTool(
        IShellHost shellHost,
        ShellSettings shellSettings,
        IShellSettingsManager shellSettingsManager,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IEnumerable<DatabaseProvider> databaseProviders)
    {
        _shellHost = shellHost;
        _shellSettings = shellSettings;
        _shellSettingsManager = shellSettingsManager;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _databaseProviders = databaseProviders;
        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
           """
            {
                "type": "object",
                "properties": {
                    "name": {
                        "type": "string",
                        "description": "A unique name for the tenant to be used as identifier."
                    },
                    "databaseProvider": {
                        "type": "string",
                        "description": "The database provider to use.",
                        "enum": [
                            "SqlConnection",
                            "MySql",
                            "Sqlite",
                            "Postgres"
                        ]
                    },
                    "requestUrlPrefix": {
                        "type": "string",
                        "description": "A URI prefix to use."
                    },
                    "requestUrlHost": {
                        "type": "string",
                        "description": "One or more qualified domain to use with this tenant."
                    },
                    "connectionString": {
                        "type": "string",
                        "description": "The connection string to use when setting up the tenant."
                    },
                    "tablePrefix": {
                        "type": "string",
                        "description": "A SQL table prefix to use for every table."
                    },
                    "recipeName": {
                        "type": "string",
                        "description": "The name of the startup recipe to use during setup."
                    }
                },
                "additionalProperties": false,
                "required": [
                    "name",
                    "recipeName"]
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Creates a new tenant or site in an uninitialized state, requiring setup before it becomes live and available.";

    public override JsonElement JsonSchema { get; }

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

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

        if (!arguments.TryGetFirstString("recipeName", out var recipeName))
        {
            return "Unable to find a recipeName argument in the function arguments.";
        }

        if (!arguments.TryGetFirstString("databaseProvider", out var databaseProvider) ||
            !_databaseProviders.Any(x => x.Name == databaseProvider))
        {
            databaseProvider = _databaseProviders.FirstOrDefault(x => x.IsDefault)?.Name;
        }

        if (_shellHost.TryGetSettings(name, out var _))
        {
            return "A tenant with the same name already exists.";
        }

        using var shellSettings = _shellSettingsManager
                .CreateDefaultSettings()
                .AsUninitialized()
                .AsDisposable();

        shellSettings.Name = name;
        shellSettings["DatabaseProvider"] = databaseProvider;
        shellSettings["RecipeName"] = recipeName;

        if (arguments.TryGetFirstString("requestUrlHost", out var requestUrlHost))
        {
            shellSettings.RequestUrlHost = requestUrlHost;
        }

        if (arguments.TryGetFirstString("requestUrlPrefix", out var requestUrlPrefix))
        {
            shellSettings.RequestUrlPrefix = requestUrlPrefix;
        }

        if (string.IsNullOrEmpty(shellSettings.RequestUrlPrefix) && string.IsNullOrEmpty(shellSettings.RequestUrlHost))
        {
            return "The requestUrlHost or requestUrlPrefix argument must be provided.";
        }

        if (arguments.TryGetFirstString("category", out var category))
        {
            shellSettings["Category"] = category;
        }

        if (arguments.TryGetFirstString("description", out var description))
        {
            shellSettings["Description"] = description;
        }

        if (arguments.TryGetFirstString("connectionString", out var connectionString))
        {
            shellSettings["ConnectionString"] = connectionString;
        }

        if (arguments.TryGetFirstString("tablePrefix", out var tablePrefix))
        {
            shellSettings["TablePrefix"] = tablePrefix;
        }

        if (arguments.TryGetFirstString("schema", out var schema))
        {
            shellSettings["Schema"] = schema;
        }

        await _shellHost.UpdateShellSettingsAsync(shellSettings);

        return $"The tenant {name} was created successfully.";
    }
}
