using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using OrchardCore.Environment.Shell;
using OrchardCore.Tenants;

namespace CrestApps.OrchardCore.AI.Tools.Tenants;

public sealed class CreateTenantOrchardCoreTool : AIFunction
{
    public const string TheName = "createOrchardCoreTenant";

    private readonly IShellHost _shellHost;
    private readonly ShellSettings _shellSettings;
    private readonly IShellSettingsManager _shellSettingsManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public CreateTenantOrchardCoreTool(
        IShellHost shellHost,
        ShellSettings shellSettings,
        IShellSettingsManager shellSettingsManager,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _shellHost = shellHost;
        _shellSettings = shellSettings;
        _shellSettingsManager = shellSettingsManager;
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
                    "recipeName",
                    "databaseProvider"]
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Creates new site in a SaaS environment.";

    public override JsonElement JsonSchema { get; }

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

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

        if (!arguments.TryGetValue("databaseProvider", out var databaseProviderArg))
        {
            return "Unable to find a databaseProvider argument in the function arguments.";
        }

        if (!arguments.TryGetValue("recipeName", out var recipeNameArg))
        {
            return "Unable to find a recipeName argument in the function arguments.";
        }

        var name = ToolHelpers.GetStringValue(nameArg);

        if (string.IsNullOrEmpty(name))
        {
            return "The name argument is required.";
        }

        var databaseProvider = ToolHelpers.GetStringValue(databaseProviderArg);

        if (string.IsNullOrEmpty(databaseProvider))
        {
            return "The databaseProvider argument is required.";
        }

        var recipeName = ToolHelpers.GetStringValue(recipeNameArg);

        if (string.IsNullOrEmpty(recipeName))
        {
            return "The recipeName argument is required.";
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

        if (!arguments.TryGetValue("requestUrlHost", out var requestUrlHostArg))
        {
            shellSettings.RequestUrlHost = ToolHelpers.GetStringValue(requestUrlHostArg);
        }

        if (!arguments.TryGetValue("requestUrlPrefix", out var requestUrlPrefixArg))
        {
            shellSettings.RequestUrlPrefix = ToolHelpers.GetStringValue(requestUrlPrefixArg);
        }

        if (!arguments.TryGetValue("category", out var categoryArg))
        {
            shellSettings["Category"] = ToolHelpers.GetStringValue(categoryArg);
        }

        if (!arguments.TryGetValue("description", out var descriptionArg))
        {
            shellSettings["Description"] = ToolHelpers.GetStringValue(descriptionArg);
        }

        if (!arguments.TryGetValue("connectionString", out var connectionStringArg))
        {
            shellSettings["ConnectionString"] = ToolHelpers.GetStringValue(connectionStringArg);
        }

        if (!arguments.TryGetValue("tablePrefix", out var tablePrefixArg))
        {
            shellSettings["TablePrefix"] = ToolHelpers.GetStringValue(tablePrefixArg);
        }

        if (!arguments.TryGetValue("schema", out var schemaArg))
        {
            shellSettings["Schema"] = ToolHelpers.GetStringValue(schemaArg);
        }

        await _shellHost.UpdateShellSettingsAsync(shellSettings);

        return $"The tenant {name} was created successfully.";
    }
}
