using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OrchardCore.Abstractions.Setup;
using OrchardCore.Email;
using OrchardCore.Environment.Shell;
using OrchardCore.Setup.Services;

namespace CrestApps.OrchardCore.AI.Agents.Tenants;

public sealed class CreateTenantTool : AIFunction
{
    public const string TheName = "createTenant";

    private readonly IShellHost _shellHost;
    private readonly ShellSettings _shellSettings;
    private readonly IShellSettingsManager _shellSettingsManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public CreateTenantTool(
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

    public override string Description => "Creates new tenant or site.";

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

public sealed class SetupTenantTool : AIFunction
{
    public const string TheName = "createTenant";

    private readonly IShellHost _shellHost;
    private readonly ShellSettings _shellSettings;
    private readonly IShellSettingsManager _shellSettingsManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISetupService _setupService;
    private readonly IdentityOptions _identityOptions;
    private readonly IEmailAddressValidator _emailAddressValidator;
    private readonly IAuthorizationService _authorizationService;

    public SetupTenantTool(
        IShellHost shellHost,
        ShellSettings shellSettings,
        IShellSettingsManager shellSettingsManager,
        IHttpContextAccessor httpContextAccessor,
        ISetupService setupService,
        IOptions<IdentityOptions> identityOptions,
        IEmailAddressValidator emailAddressValidator,
        IAuthorizationService authorizationService)
    {
        _shellHost = shellHost;
        _shellSettings = shellSettings;
        _shellSettingsManager = shellSettingsManager;
        _httpContextAccessor = httpContextAccessor;
        _setupService = setupService;
        _identityOptions = identityOptions.Value;
        _emailAddressValidator = emailAddressValidator;
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
                    "username": {
                        "type": "string",
                        "description": "The username for the super user to setup the site with."
                    },
                    "email": {
                        "type": "string",
                        "description": "A valid email for the super user to setup the site with."
                    },
                    "password": {
                        "type": "string",
                        "description": "The password for the super user to setup the site with."
                    },
                    "title": {
                        "type": "string",
                        "description": "A title for the site."
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
                    "username",
                    "email",
                    "password"]
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Setups up a tenant.";

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

        if (!arguments.TryGetValue("name", out var nameArg))
        {
            return "Unable to find a name argument in the function arguments.";
        }

        if (!arguments.TryGetValue("username", out var usernameArg))
        {
            return "Unable to find a username argument in the function arguments.";
        }

        if (!arguments.TryGetValue("email", out var emailArg))
        {
            return "Unable to find a email argument in the function arguments.";
        }

        if (!arguments.TryGetValue("password", out var passwordArg))
        {
            return "Unable to find a password argument in the function arguments.";
        }

        var name = ToolHelpers.GetStringValue(nameArg);

        if (string.IsNullOrEmpty(name))
        {
            return "The name argument is required.";
        }

        if (!_shellHost.TryGetSettings(name, out var tenantSettings))
        {
            return "Invalid tenant name provided.";
        }

        if (!tenantSettings.IsUninitialized())
        {
            return "The tenant is already setup.";
        }

        var username = ToolHelpers.GetStringValue(usernameArg);

        if (string.IsNullOrEmpty(username))
        {
            return "The username argument is required.";
        }

        if (username.Any(c => !_identityOptions.User.AllowedUserNameCharacters.Contains(c)))
        {
            return $"The username contains not allowed characters. Allowed characters are: {string.Join(' ', _identityOptions.User.AllowedUserNameCharacters)}";
        }

        var password = ToolHelpers.GetStringValue(passwordArg);

        if (string.IsNullOrEmpty(password))
        {
            return "The password argument is required.";
        }

        var email = ToolHelpers.GetStringValue(emailArg);

        if (string.IsNullOrEmpty(email))
        {
            return "The email argument is required.";
        }
        else if (!_emailAddressValidator.Validate(email))
        {
            return $"The email is invalid.";
        }

        var recipeName = tenantSettings["RecipeName"];

        if (arguments.TryGetValue("recipeName", out var recipeNameArg))
        {
            recipeName = ToolHelpers.GetStringValue(recipeNameArg);
        }

        if (string.IsNullOrEmpty(recipeName))
        {
            return "The recipeName argument is required.";
        }

        var recipe = (await _setupService.GetSetupRecipesAsync()).FirstOrDefault(x => x.Name == recipeName);

        if (recipe is null)
        {
            return "The recipe name is invalid.";
        }

        var databaseProvider = tenantSettings["DatabaseProvider"];

        if (!arguments.TryGetValue("databaseProvider", out var databaseProviderArg))
        {
            databaseProvider = ToolHelpers.GetStringValue(databaseProviderArg);
        }

        if (string.IsNullOrEmpty(databaseProvider))
        {
            return "The databaseProvider argument is required.";
        }

        var requestUrlHost = tenantSettings.RequestUrlHost;

        if (!arguments.TryGetValue("requestUrlHost", out var requestUrlHostArg))
        {
            requestUrlHost = ToolHelpers.GetStringValue(requestUrlHostArg);
        }

        var requestUrlPrefix = tenantSettings.RequestUrlPrefix;

        if (!arguments.TryGetValue("requestUrlPrefix", out var requestUrlPrefixArg))
        {
            requestUrlPrefix = ToolHelpers.GetStringValue(requestUrlPrefixArg);
        }

        if (string.IsNullOrEmpty(requestUrlPrefix) && string.IsNullOrEmpty(requestUrlHost))
        {
            return "The requestUrlHost or requestUrlPrefix argument must be provided.";
        }

        var connectionString = tenantSettings["ConnectionString"];
        if (!arguments.TryGetValue("connectionString", out var connectionStringArg))
        {
            connectionString = ToolHelpers.GetStringValue(connectionStringArg);
        }

        var category = tenantSettings["Category"];

        if (!arguments.TryGetValue("category", out var categoryArg))
        {
            category = ToolHelpers.GetStringValue(categoryArg);
        }

        var title = tenantSettings.Name;

        if (!arguments.TryGetValue("title", out var titleArg))
        {
            title = ToolHelpers.GetStringValue(titleArg);
        }

        var description = tenantSettings["Description"];

        if (!arguments.TryGetValue("description", out var descriptionArg))
        {
            description = ToolHelpers.GetStringValue(descriptionArg);
        }

        var tablePrefix = tenantSettings["TablePrefix"];
        if (!arguments.TryGetValue("tablePrefix", out var tablePrefixArg))
        {
            tablePrefix = ToolHelpers.GetStringValue(tablePrefixArg);
        }

        var schema = tenantSettings["Schema"];

        if (!arguments.TryGetValue("schema", out var schemaArg))
        {
            schema = ToolHelpers.GetStringValue(schemaArg);
        }

        var setupContext = new SetupContext
        {
            ShellSettings = _shellSettings,
            EnabledFeatures = null, // default list,
            Errors = new Dictionary<string, string>(),
            Recipe = recipe,
            Properties = new Dictionary<string, object>
            {
                { SetupConstants.SiteName, title },
                { SetupConstants.AdminUsername, username },
                { SetupConstants.AdminEmail, email },
                { SetupConstants.AdminPassword, password },
                // { SetupConstants.SiteTimeZone, model.SiteTimeZone },
            },
        };

        if (!string.IsNullOrEmpty(connectionString))
        {
            setupContext.Properties[SetupConstants.DatabaseProvider] = tenantSettings["DatabaseProvider"];
            setupContext.Properties[SetupConstants.DatabaseConnectionString] = tenantSettings["ConnectionString"];
            setupContext.Properties[SetupConstants.DatabaseTablePrefix] = tenantSettings["TablePrefix"];
            setupContext.Properties[SetupConstants.DatabaseSchema] = tenantSettings["Schema"];
        }
        else
        {
            setupContext.Properties[SetupConstants.DatabaseProvider] = databaseProvider;
            setupContext.Properties[SetupConstants.DatabaseConnectionString] = null;
            setupContext.Properties[SetupConstants.DatabaseTablePrefix] = tablePrefix;
            setupContext.Properties[SetupConstants.DatabaseSchema] = schema;
        }

        var executionId = await _setupService.SetupAsync(setupContext);

        // Check if any Setup component failed (e.g., database connection validation)
        if (setupContext.Errors.Count > 0)
        {
            var builder = new StringBuilder("Failed to setup the tenant due to the following errors:");

            foreach (var error in setupContext.Errors)
            {
                builder.AppendLine($"{error.Key}: {error.Value}");
            }

            return builder.ToString();
        }

        return $"The tenant {name} was setup successfully.";
    }
}
