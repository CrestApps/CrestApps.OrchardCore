using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OrchardCore.Abstractions.Setup;
using OrchardCore.Email;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Setup.Services;

namespace CrestApps.OrchardCore.AI.Agents.Tenants;

public sealed class SetupTenantTool : AIFunction
{
    public const string TheName = "setupTenant";

    private readonly IShellHost _shellHost;
    private readonly ShellSettings _shellSettings;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISetupService _setupService;
    private readonly IdentityOptions _identityOptions;
    private readonly IEmailAddressValidator _emailAddressValidator;
    private readonly IAuthorizationService _authorizationService;
    private readonly IClock _clock;

    public SetupTenantTool(
        IShellHost shellHost,
        ShellSettings shellSettings,
        IHttpContextAccessor httpContextAccessor,
        ISetupService setupService,
        IOptions<IdentityOptions> identityOptions,
        IEmailAddressValidator emailAddressValidator,
        IAuthorizationService authorizationService,
        IClock clock)
    {
        _shellHost = shellHost;
        _shellSettings = shellSettings;
        _httpContextAccessor = httpContextAccessor;
        _setupService = setupService;
        _identityOptions = identityOptions.Value;
        _emailAddressValidator = emailAddressValidator;
        _authorizationService = authorizationService;
        _clock = clock;
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
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Completes the setup of an uninitialized tenant, bringing the site online and making it available for incoming requests.";

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

        if (!_shellHost.TryGetSettings(name, out var tenantSettings))
        {
            return "Invalid tenant name provided.";
        }

        if (!arguments.TryGetFirstString("username", out var username))
        {
            return "Unable to find a username argument in the function arguments.";
        }

        if (!arguments.TryGetFirstString("email", out var email))
        {
            return "Unable to find a email argument in the function arguments.";
        }

        if (!arguments.TryGetFirstString("password", out var password))
        {
            return "Unable to find a password argument in the function arguments.";
        }

        if (!tenantSettings.IsUninitialized())
        {
            return "The tenant is already setup.";
        }

        if (username.Any(c => !_identityOptions.User.AllowedUserNameCharacters.Contains(c)))
        {
            return $"The username contains not allowed characters. Allowed characters are: {string.Join(' ', _identityOptions.User.AllowedUserNameCharacters)}";
        }

        if (!_emailAddressValidator.Validate(email))
        {
            return $"The email is invalid.";
        }

        var recipeName = arguments.GetValueOrDefault("recipeName", tenantSettings["RecipeName"]);

        if (string.IsNullOrEmpty(recipeName))
        {
            return "The recipeName argument is required.";
        }

        var recipe = (await _setupService.GetSetupRecipesAsync()).FirstOrDefault(x => x.Name == recipeName);

        if (recipe is null)
        {
            return "The recipe name is invalid.";
        }

        var databaseProvider = arguments.GetValueOrDefault("databaseProvider", tenantSettings["DatabaseProvider"]);

        if (string.IsNullOrEmpty(databaseProvider))
        {
            return "The databaseProvider argument is required.";
        }

        var requestUrlHost = arguments.GetValueOrDefault("requestUrlHost", tenantSettings.RequestUrlHost);
        var requestUrlPrefix = arguments.GetValueOrDefault("requestUrlPrefix", tenantSettings.RequestUrlPrefix);

        if (string.IsNullOrEmpty(requestUrlPrefix) && string.IsNullOrEmpty(requestUrlHost))
        {
            return "The requestUrlHost or requestUrlPrefix argument must be provided.";
        }

        tenantSettings["ConnectionString"] = arguments.GetValueOrDefault("connectionString", tenantSettings["ConnectionString"]);

        tenantSettings["Category"] = arguments.GetValueOrDefault("category", tenantSettings["Category"]);

        tenantSettings["Description"] = arguments.GetValueOrDefault("description", tenantSettings["Description"]);

        tenantSettings["TablePrefix"] = arguments.GetValueOrDefault("tablePrefix", tenantSettings["TablePrefix"]);

        tenantSettings["Schema"] = arguments.GetValueOrDefault("schema", tenantSettings["Schema"]);

        string timeZoneId = null;

        if (arguments.TryGetFirstString("timeZoneId", out var id))
        {
            var zone = _clock.GetTimeZones()
                .FirstOrDefault(x => x.TimeZoneId.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (zone is not null)
            {
                timeZoneId = zone.TimeZoneId;
            }
        }

        timeZoneId ??= _clock.GetSystemTimeZone().TimeZoneId;

        var setupContext = new SetupContext
        {
            ShellSettings = tenantSettings,
            EnabledFeatures = null, // default list.
            Errors = new Dictionary<string, string>(),
            Recipe = recipe,
            Properties = new Dictionary<string, object>
            {
                { SetupConstants.SiteName, arguments.GetValueOrDefault("title", tenantSettings.Name) },
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

        var executionId = await _setupService.SetupAsync(setupContext);

        // Check if any Setup component failed (e.g., database connection validation).
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
