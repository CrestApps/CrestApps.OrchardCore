using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Agent.Tenants;

public sealed class CreateTenantTool : AIFunction
{
    public const string TheName = "createTenant";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
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
        """);

    public override string Name => TheName;

    public override string Description => "Creates a new tenant or site in an uninitialized state, requiring setup before it becomes live and available.";

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
        var shellSettingsManager = arguments.Services.GetRequiredService<IShellSettingsManager>();
        var httpContextAccessor = arguments.Services.GetRequiredService<IHttpContextAccessor>();
        var authorizationService = arguments.Services.GetRequiredService<IAuthorizationService>();
        var databaseProviders = arguments.Services.GetRequiredService<IEnumerable<DatabaseProvider>>();

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, OrchardCorePermissions.ManageTenants))
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

        if (!arguments.TryGetFirstString("recipeName", out var recipeName))
        {
            return "Unable to find a recipeName argument in the function arguments.";
        }

        if (!arguments.TryGetFirstString("databaseProvider", out var databaseProvider) ||
            !databaseProviders.Any(x => x.Name == databaseProvider))
        {
            databaseProvider = databaseProviders.FirstOrDefault(x => x.IsDefault)?.Name;
        }

        if (shellHost.TryGetSettings(name, out var _))
        {
            return "A tenant with the same name already exists.";
        }

        using var newShellSettings = shellSettingsManager
                .CreateDefaultSettings()
                .AsUninitialized()
                .AsDisposable();

        newShellSettings.Name = name;
        newShellSettings["DatabaseProvider"] = databaseProvider;
        newShellSettings["RecipeName"] = recipeName;

        if (arguments.TryGetFirstString("requestUrlHost", out var requestUrlHost))
        {
            newShellSettings.RequestUrlHost = requestUrlHost;
        }

        if (arguments.TryGetFirstString("requestUrlPrefix", out var requestUrlPrefix))
        {
            newShellSettings.RequestUrlPrefix = requestUrlPrefix;
        }

        if (string.IsNullOrEmpty(newShellSettings.RequestUrlPrefix) && string.IsNullOrEmpty(newShellSettings.RequestUrlHost))
        {
            return "The requestUrlHost or requestUrlPrefix argument must be provided.";
        }

        if (arguments.TryGetFirstString("category", out var category))
        {
            newShellSettings["Category"] = category;
        }

        if (arguments.TryGetFirstString("description", out var description))
        {
            newShellSettings["Description"] = description;
        }

        if (arguments.TryGetFirstString("connectionString", out var connectionString))
        {
            newShellSettings["ConnectionString"] = connectionString;
        }

        if (arguments.TryGetFirstString("tablePrefix", out var tablePrefix))
        {
            newShellSettings["TablePrefix"] = tablePrefix;
        }

        if (arguments.TryGetFirstString("schema", out var schema))
        {
            newShellSettings["Schema"] = schema;
        }

        await shellHost.UpdateShellSettingsAsync(newShellSettings);

        return $"The tenant {name} was created successfully.";
    }
}
