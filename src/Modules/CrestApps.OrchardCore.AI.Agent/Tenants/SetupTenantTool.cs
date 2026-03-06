using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Cysharp.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Abstractions.Setup;
using OrchardCore.Email;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Setup.Services;

namespace CrestApps.OrchardCore.AI.Agent.Tenants;

public sealed class SetupTenantTool : AIFunction
{
    public const string TheName = "setupTenant";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
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
                "timeZoneId": {
                    "type": "string",
                    "description": "The Unix TimeZone id."
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
        """);

    public override string Name => TheName;

    public override string Description => "Completes the setup of an uninitialized tenant, bringing the site online and making it available for incoming requests.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<SetupTenantTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", Name);
        }

        var shellHost = arguments.Services.GetRequiredService<IShellHost>();
        var shellSettings = arguments.Services.GetRequiredService<ShellSettings>();
        var setupService = arguments.Services.GetRequiredService<ISetupService>();
        var identityOptions = arguments.Services.GetRequiredService<IOptions<IdentityOptions>>().Value;
        var emailAddressValidator = arguments.Services.GetRequiredService<IEmailAddressValidator>();
        var clock = arguments.Services.GetRequiredService<IClock>();

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

            return "Invalid tenant name provided.";
        }

        if (!arguments.TryGetFirstString("username", out var username))
        {
            logger.LogWarning("AI tool '{ToolName}' failed: missing 'username' argument.", Name);

            return "Unable to find a username argument in the function arguments.";
        }

        if (!arguments.TryGetFirstString("email", out var email))
        {
            logger.LogWarning("AI tool '{ToolName}' failed: missing 'email' argument.", Name);

            return "Unable to find a email argument in the function arguments.";
        }

        if (!arguments.TryGetFirstString("password", out var password))
        {
            logger.LogWarning("AI tool '{ToolName}' failed: missing 'password' argument.", Name);

            return "Unable to find a password argument in the function arguments.";
        }

        if (!tenantSettings.IsUninitialized())
        {
            logger.LogWarning("AI tool '{ToolName}' failed: tenant '{TenantName}' is already setup.", Name, name);

            return "The tenant is already setup.";
        }

        if (username.Any(c => !identityOptions.User.AllowedUserNameCharacters.Contains(c)))
        {
            logger.LogWarning("AI tool '{ToolName}' failed: username contains invalid characters for tenant '{TenantName}'.", Name, name);

            return $"The username contains not allowed characters. Allowed characters are: {string.Join(' ', identityOptions.User.AllowedUserNameCharacters)}";
        }

        if (!emailAddressValidator.Validate(email))
        {
            logger.LogWarning("AI tool '{ToolName}' failed: invalid email provided for tenant '{TenantName}'.", Name, name);

            return $"The email is invalid.";
        }

        var recipeName = arguments.GetFirstValueOrDefault("recipeName", tenantSettings["RecipeName"]);

        if (string.IsNullOrEmpty(recipeName))
        {
            logger.LogWarning("AI tool '{ToolName}' failed: missing 'recipeName' argument for tenant '{TenantName}'.", Name, name);

            return "The recipeName argument is required.";
        }

        var recipe = (await setupService.GetSetupRecipesAsync()).FirstOrDefault(x => x.Name == recipeName);

        if (recipe is null)
        {
            logger.LogWarning("AI tool '{ToolName}' failed: recipe '{RecipeName}' not found for tenant '{TenantName}'.", Name, recipeName, name);

            return "The recipe name is invalid.";
        }

        var databaseProvider = arguments.GetFirstValueOrDefault("databaseProvider", tenantSettings["DatabaseProvider"]);

        if (string.IsNullOrEmpty(databaseProvider))
        {
            logger.LogWarning("AI tool '{ToolName}' failed: missing 'databaseProvider' argument for tenant '{TenantName}'.", Name, name);

            return "The databaseProvider argument is required.";
        }

        var requestUrlHost = arguments.GetFirstValueOrDefault("requestUrlHost", tenantSettings.RequestUrlHost);
        var requestUrlPrefix = arguments.GetFirstValueOrDefault("requestUrlPrefix", tenantSettings.RequestUrlPrefix);

        if (string.IsNullOrEmpty(requestUrlPrefix) && string.IsNullOrEmpty(requestUrlHost))
        {
            logger.LogWarning("AI tool '{ToolName}' failed: neither 'requestUrlHost' nor 'requestUrlPrefix' was provided for tenant '{TenantName}'.", Name, name);

            return "The requestUrlHost or requestUrlPrefix argument must be provided.";
        }

        tenantSettings["ConnectionString"] = arguments.GetFirstValueOrDefault("connectionString", tenantSettings["ConnectionString"]);

        tenantSettings["Category"] = arguments.GetFirstValueOrDefault("category", tenantSettings["Category"]);

        tenantSettings["Description"] = arguments.GetFirstValueOrDefault("description", tenantSettings["Description"]);

        tenantSettings["TablePrefix"] = arguments.GetFirstValueOrDefault("tablePrefix", tenantSettings["TablePrefix"]);

        tenantSettings["Schema"] = arguments.GetFirstValueOrDefault("schema", tenantSettings["Schema"]);

        string timeZoneId = null;

        if (arguments.TryGetFirstString("timeZoneId", out var id))
        {
            var zone = clock.GetTimeZones()
                .FirstOrDefault(x => x.TimeZoneId.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (zone is not null)
            {
                timeZoneId = zone.TimeZoneId;
            }
        }

        timeZoneId ??= clock.GetSystemTimeZone().TimeZoneId;

        var setupContext = new SetupContext
        {
            ShellSettings = tenantSettings,
            EnabledFeatures = null, // default list.
            Errors = new Dictionary<string, string>(),
            Recipe = recipe,
            Properties = new Dictionary<string, object>
            {
                { SetupConstants.SiteName, arguments.GetFirstValueOrDefault("title", tenantSettings.Name) },
                { SetupConstants.AdminUsername, username },
                { SetupConstants.AdminEmail, email },
                { SetupConstants.AdminPassword, password },
                { SetupConstants.SiteTimeZone, timeZoneId },
            },
        };

        if (!string.IsNullOrEmpty(tenantSettings["ConnectionString"]))
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
            setupContext.Properties[SetupConstants.DatabaseTablePrefix] = tenantSettings["TablePrefix"];
            setupContext.Properties[SetupConstants.DatabaseSchema] = tenantSettings["Schema"];
        }

        var executionId = await setupService.SetupAsync(setupContext);

        // Check if any Setup component failed (e.g., database connection validation).
        if (setupContext.Errors.Count > 0)
        {
            logger.LogWarning("AI tool '{ToolName}' failed: setup of tenant '{TenantName}' encountered errors.", Name, name);

            using var builder = ZString.CreateStringBuilder();
            builder.Append("Failed to setup the tenant due to the following errors:");

            foreach (var error in setupContext.Errors)
            {
                builder.Append(error.Key);
                builder.Append(": ");
                builder.AppendLine(error.Value);
            }

            return builder.ToString();
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' completed.", Name);
        }

        return $"The tenant {name} was setup successfully.";
    }
}
