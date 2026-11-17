using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OrchardCore.OpenId;
using OrchardCore.OpenId.Abstractions.Managers;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Security;
using OrchardCore.Security.Services;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.AI.Agent.OpenId;

/// <summary>
/// Creates a public OpenID Connect client application, or changes an application, without ever taking or writing a
/// client secret. The Applications admin page turns its form into the permissions of the application with a mapping
/// that is internal to the OpenID module; the only public way to that mapping is the module's <c>OpenIdApplication</c>
/// recipe step, which the Authorization Server feature registers, so the tool builds that step from its checked
/// arguments and runs it. The step keeps the stored secret of a confidential client when it is given none. The tool
/// adds the rules a chat needs: a role is a privilege grant, so each role it adds needs the permission to assign that
/// role and the administrator role is refused; a new redirect address must be absolute https; and the implicit and
/// password flows must be confirmed in the call so the person who approves sees them.
/// </summary>
public sealed class SaveOpenIdApplicationTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveOpenIdApplication";

    // The name of the recipe step of the OpenID module that creates or updates one application.
    private const string StepName = "OpenIdApplication";

    // The field of a role or scope entry of the step that holds its name.
    private const string EntryNameField = "Name";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The id of the application to update, from listOpenIdApplications. Leave it out to create an application."
        },
        "clientId": {
          "type": "string",
          "description": "For a new application: its client id, which no other application may have. For an update it only confirms which application is meant; this tool does not change a client id."
        },
        "displayName": {
          "type": "string",
          "description": "The display name. It is required for a new application."
        },
        "type": {
          "type": "string",
          "enum": [
            "public",
            "confidential"
          ],
          "description": "Leave it out: a new application is a public client and an update keeps the type. Giving confidential for a new application, or a type that differs from the stored one, is refused, because a confidential client needs a client secret that only a person may type on the Applications admin page."
        },
        "consentType": {
          "type": "string",
          "enum": [
            "explicit",
            "implicit",
            "external"
          ],
          "description": "How users consent to the application. The default for a new application is explicit."
        },
        "redirectUris": {
          "type": "array",
          "description": "The complete list of redirect addresses; it replaces the stored list. Each address this call adds must be an absolute https address without a fragment. Leave it out to keep the stored list.",
          "items": {
            "type": "string"
          }
        },
        "postLogoutRedirectUris": {
          "type": "array",
          "description": "The complete list of post-logout redirect addresses; it replaces the stored list. Each address this call adds must be an absolute https address without a fragment. Leave it out to keep the stored list.",
          "items": {
            "type": "string"
          }
        },
        "allowedFlows": {
          "type": "array",
          "description": "The complete list of flows the application may use; it replaces the stored list. The implicit and password flows are discouraged and must be confirmed with confirmDiscouragedFlows. Leave it out to keep the stored flows.",
          "items": {
            "type": "string",
            "enum": [
              "authorizationCode",
              "clientCredentials",
              "hybrid",
              "implicit",
              "password",
              "refreshToken"
            ]
          }
        },
        "allowedEndpoints": {
          "type": "array",
          "description": "The complete list of extra endpoints the application may call; it replaces the stored list. Leave it out to keep the stored endpoints.",
          "items": {
            "type": "string",
            "enum": [
              "logout",
              "introspection",
              "revocation"
            ]
          }
        },
        "requirePkce": {
          "type": "boolean",
          "description": "Whether the application must use PKCE with the authorization code flow. Use true for a public client."
        },
        "requirePushedAuthorizationRequests": {
          "type": "boolean",
          "description": "Whether the application must use pushed authorization requests."
        },
        "roles": {
          "type": "array",
          "description": "The complete list of roles the application acts with in the client credentials flow; it replaces the stored list. Each role this call adds needs the permission to assign that role, and the administrator role is refused. Leave it out to keep the stored roles.",
          "items": {
            "type": "string"
          }
        },
        "scopes": {
          "type": "array",
          "description": "The complete list of scopes the application may request, by name from listOpenIdScopes; it replaces the stored list. Leave it out to keep the stored scopes.",
          "items": {
            "type": "string"
          }
        },
        "confirmDiscouragedFlows": {
          "type": "boolean",
          "description": "True to confirm that the person asked for the implicit or the password flow, after telling them that current OAuth guidance discourages both. The call is refused when it turns one of them on without this."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a public client application or updates an existing application's display name, redirect URIs, flows, endpoints, requirements, roles and scopes. It cannot set or rotate a client secret, so creating a confidential client or turning a public client into a confidential one must be done on the Applications page. It refuses a new redirect address that is not absolute https, the administrator role, and a role the caller may not assign.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, OpenIdPermissions.ManageApplications))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!AgentToolArguments.TryReadStrings(arguments, "redirectUris", out var redirectUris) ||
            !AgentToolArguments.TryReadStrings(arguments, "postLogoutRedirectUris", out var postLogoutRedirectUris) ||
            !AgentToolArguments.TryReadStrings(arguments, "allowedFlows", out var flows) ||
            !AgentToolArguments.TryReadStrings(arguments, "allowedEndpoints", out var endpoints) ||
            !AgentToolArguments.TryReadStrings(arguments, "roles", out var roles) ||
            !AgentToolArguments.TryReadStrings(arguments, "scopes", out var scopes))
        {
            return "Nothing was saved: redirectUris, postLogoutRedirectUris, allowedFlows, allowedEndpoints, roles and scopes must each be a list of texts.";
        }

        arguments.TryGetFirstString("id", out var id);
        arguments.TryGetFirstString("clientId", out var clientId);

        var hasDisplayName = arguments.TryGetFirstString("displayName", out var displayName) && !string.IsNullOrWhiteSpace(displayName);
        var hasType = arguments.TryGetFirstString("type", out var type);
        var hasConsentType = arguments.TryGetFirstString("consentType", out var consentType);
        var hasRequirePkce = arguments.TryGetFirst<bool>("requirePkce", out var requirePkce);
        var hasRequirePar = arguments.TryGetFirst<bool>("requirePushedAuthorizationRequests", out var requirePar);
        var manager = services.GetRequiredService<IOpenIdApplicationManager>();
        var create = string.IsNullOrWhiteSpace(id);
        OpenIdApplicationState current;

        if (create)
        {
            clientId = clientId?.Trim();

            if (string.IsNullOrEmpty(clientId))
            {
                return "Nothing was saved: give clientId for a new application, or the id of the application to update.";
            }

            // The check of the Create page.
            if (await manager.FindByClientIdAsync(clientId, cancellationToken) is { } taken)
            {
                return $"Nothing was saved: the client id '{clientId}' is taken by the application with the id '{await manager.GetPhysicalIdAsync(taken, cancellationToken)}'. Give that id to update it, or pick another client id.";
            }

            if (!hasDisplayName)
            {
                return "Nothing was saved: give displayName for a new application.";
            }

            // A confidential client needs a secret at creation, and a secret never passes through the chat.
            if (hasType && !IsPublic(type))
            {
                return $"Nothing was saved: this tool creates public clients only. A confidential client needs a client secret, which a person types on the Applications admin page{FormatLink(OpenIdToolSupport.GetApplicationsUrl(services))}. Never ask for the secret in the chat.";
            }

            current = new OpenIdApplicationState
            {
                ClientId = clientId,
                Type = OpenIddictConstants.ClientTypes.Public,
                ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
            };
        }
        else
        {
            var (application, error) = await OpenIdToolSupport.FindAsync(manager, id, clientId, cancellationToken);

            if (error is not null)
            {
                return $"Nothing was saved: {error}";
            }

            current = await OpenIdToolSupport.ReadAsync(manager, application, cancellationToken);

            // Public to confidential needs a new secret; confidential to public deletes the stored one.
            if (hasType && !string.Equals(type.Trim(), current.Type, StringComparison.OrdinalIgnoreCase))
            {
                return $"Nothing was saved: this tool does not change the type of an application. The application is a {current.Type} client; changing the type sets or removes its client secret, which a person does on the Applications admin page{FormatLink(OpenIdToolSupport.GetApplicationUrl(services, current.Id))}.";
            }

            if (!hasDisplayName &&
                !hasConsentType &&
                !hasRequirePkce &&
                !hasRequirePar &&
                redirectUris is null &&
                postLogoutRedirectUris is null &&
                flows is null &&
                endpoints is null &&
                roles is null &&
                scopes is null)
            {
                return "Nothing was saved: give at least one field to change.";
            }
        }

        var target = new OpenIdApplicationState
        {
            Id = current.Id,
            ClientId = current.ClientId,
            Type = current.Type,
            DisplayName = hasDisplayName
                ? displayName.Trim()
                : current.DisplayName,
            ConsentType = current.ConsentType,
            RedirectUris = redirectUris ?? current.RedirectUris,
            PostLogoutRedirectUris = postLogoutRedirectUris ?? current.PostLogoutRedirectUris,
            Flows = current.Flows,
            Endpoints = current.Endpoints,
            RequirePkce = hasRequirePkce
                ? requirePkce
                : current.RequirePkce,
            RequirePushedAuthorizationRequests = hasRequirePar
                ? requirePar
                : current.RequirePushedAuthorizationRequests,
            Roles = current.Roles,
            Scopes = scopes ?? current.Scopes,
        };

        if (hasConsentType)
        {
            target.ConsentType = OpenIdToolSupport.ConsentTypes.FirstOrDefault(known => string.Equals(known, consentType.Trim(), StringComparison.OrdinalIgnoreCase));

            if (target.ConsentType is null)
            {
                return $"Nothing was saved: consentType must be one of {string.Join(", ", OpenIdToolSupport.ConsentTypes)}.";
            }
        }

        if (flows is not null)
        {
            if (!TryMatch(flows, OpenIdToolSupport.Flows, out var matched, out var unknownFlow))
            {
                return $"Nothing was saved: '{unknownFlow}' is not a flow. The flows are {string.Join(", ", OpenIdToolSupport.Flows)}.";
            }

            target.Flows = matched;
        }

        if (endpoints is not null)
        {
            if (!TryMatch(endpoints, OpenIdToolSupport.Endpoints, out var matched, out var unknownEndpoint))
            {
                return $"Nothing was saved: '{unknownEndpoint}' is not an endpoint. The endpoints are {string.Join(", ", OpenIdToolSupport.Endpoints)}.";
            }

            target.Endpoints = matched;
        }

        // The implicit flow returns tokens in the address of the browser and the password flow gives the application
        // the password of the user. The person who approves must see that one of them is turned on.
        var discouraged = target.Flows
            .Where(flow => flow is OpenIdToolSupport.ImplicitFlow or OpenIdToolSupport.PasswordFlow)
            .Where(flow => !current.Flows.Contains(flow))
            .ToList();

        if (discouraged.Count > 0 && !arguments.GetFirstValueOrDefault("confirmDiscouragedFlows", false))
        {
            return $"Nothing was saved: this call turns on the {string.Join(" and the ", discouraged)} flow, which current OAuth guidance discourages (the implicit flow puts tokens in the address of the browser; the password flow hands the user's password to the application). Tell the person, and call again with confirmDiscouragedFlows set to true when they still want it.";
        }

        // An address that is already stored was entered by a person on the admin page and may stay.
        var badAddresses = target.RedirectUris.Except(current.RedirectUris, StringComparer.Ordinal)
            .Concat(target.PostLogoutRedirectUris.Except(current.PostLogoutRedirectUris, StringComparer.Ordinal))
            .Where(address => !IsAbsoluteHttps(address))
            .ToList();

        if (badAddresses.Count > 0)
        {
            return $"Nothing was saved: {OpenIdToolSupport.Quote(badAddresses)} is not an absolute https address without a fragment. This tool adds https addresses only; another address (http for a local test, the custom scheme of a mobile app) is entered by a person on the Applications admin page.";
        }

        // The admin page offers the scopes that exist.
        var scopeManager = services.GetRequiredService<IOpenIdScopeManager>();
        var unknownScopes = new List<string>();

        foreach (var scope in target.Scopes.Except(current.Scopes, StringComparer.Ordinal))
        {
            if (await scopeManager.FindByNameAsync(scope, cancellationToken) is null)
            {
                unknownScopes.Add(scope);
            }
        }

        if (unknownScopes.Count > 0)
        {
            return $"Nothing was saved: there is no scope named {OpenIdToolSupport.Quote(unknownScopes)}. Use listOpenIdScopes to get the names, or saveOpenIdScope to create the scope first.";
        }

        if (roles is not null)
        {
            var (resolvedRoles, roleError) = await ResolveRolesAsync(services, roles, current.Roles);

            if (roleError is not null)
            {
                return roleError;
            }

            target.Roles = resolvedRoles;
        }

        var context = new RecipeExecutionContext
        {
            Name = StepName,
            Step = BuildStep(target),
        };

        try
        {
            // As the recipe executor does: every handler gets the step, and the one that owns the name runs it.
            foreach (var handler in services.GetServices<IRecipeStepHandler>())
            {
                await handler.ExecuteAsync(context);
            }
        }
        catch (OpenIddictExceptions.ValidationException exception)
        {
            return $"Nothing was saved: {exception.Message}";
        }

        if (context.Errors.Count > 0)
        {
            return $"Nothing was saved: {string.Join(" ", context.Errors)}";
        }

        var saved = await manager.FindByClientIdAsync(target.ClientId, cancellationToken);

        if (saved is null)
        {
            return "Nothing was saved: the OpenID application step did not run. The OpenID Connect Authorization Server feature provides it; check that the feature is on.";
        }

        var after = await OpenIdToolSupport.ReadAsync(manager, saved, cancellationToken);
        var warnings = GetWarnings(after);

        return AgentToolResults.Json(new
        {
            saved = true,
            created = create,
            application = OpenIdToolSupport.Describe(services, after),
            changed = create
                ? null
                : GetChanges(current, after),
            warnings = warnings.Count > 0
                ? warnings
                : null,
            note = OpenIdToolSupport.IsConfidential(after)
                ? "The client secret of this confidential client was not read or changed."
                : "This is a public client: it has no client secret.",
        });
    }

    private static async Task<(List<string> Roles, string Error)> ResolveRolesAsync(IServiceProvider services, List<string> given, List<string> current)
    {
        var kept = current.Where(role => given.Contains(role, StringComparer.OrdinalIgnoreCase)).ToList();
        var added = given.Where(role => !current.Contains(role, StringComparer.OrdinalIgnoreCase)).ToList();

        if (added.Count == 0)
        {
            return (kept, null);
        }

        var roleService = services.GetService<IRoleService>();

        if (roleService is null)
        {
            return (null, "Nothing was saved: the Roles feature is off, so an application cannot be given a role.");
        }

        // A client that signs in with the client credentials flow acts with these roles, so a role is given to an
        // application under the rules for giving it to a user.
        var assignable = (await roleService.GetAssignableRolesAsync()).ToList();
        var resolved = new List<IRole>();
        var unknown = new List<string>();

        foreach (var name in added)
        {
            var role = assignable.FirstOrDefault(candidate => string.Equals(candidate.RoleName, name, StringComparison.OrdinalIgnoreCase));

            if (role is null)
            {
                unknown.Add(name);
            }
            else
            {
                resolved.Add(role);
            }
        }

        if (unknown.Count > 0)
        {
            return (null, $"Nothing was saved: {OpenIdToolSupport.Quote(unknown)} cannot be given to an application. The roles that can are: {string.Join(", ", assignable.Select(role => role.RoleName).Order(StringComparer.OrdinalIgnoreCase))}.");
        }

        foreach (var role in resolved)
        {
            if (await roleService.IsAdminRoleAsync(role.RoleName))
            {
                return (null, $"Nothing was saved: this tool does not give the administrator role '{role.RoleName}' to an application, because the client would hold every permission. Give it a role with the permissions it needs.");
            }
        }

        var notAssignable = new List<string>();

        foreach (var role in resolved)
        {
            if (!await AgentToolAuthorization.IsAuthorizedAsync(services, UsersPermissions.AssignRoleToUsers, role))
            {
                notAssignable.Add(role.RoleName);
            }
        }

        if (notAssignable.Count > 0)
        {
            return (null, $"Nothing was saved: you are not allowed to assign the role {OpenIdToolSupport.Quote(notAssignable)}.");
        }

        kept.AddRange(resolved.Select(role => role.RoleName));

        return (kept, null);
    }

    private static JsonObject BuildStep(OpenIdApplicationState target)
    {
        // The step has a field for a client secret. It is never written: an empty one keeps the stored secret of a
        // confidential client, and a public client has none.
        return new JsonObject
        {
            ["name"] = StepName,
            ["ClientId"] = target.ClientId,
            ["DisplayName"] = target.DisplayName,
            ["Type"] = target.Type,
            ["ConsentType"] = target.ConsentType,
            ["RedirectUris"] = string.Join(' ', target.RedirectUris),
            ["PostLogoutRedirectUris"] = string.Join(' ', target.PostLogoutRedirectUris),
            ["RoleEntries"] = ToEntries(target.Roles),
            ["ScopeEntries"] = ToEntries(target.Scopes),
            ["AllowAuthorizationCodeFlow"] = target.Flows.Contains(OpenIdToolSupport.AuthorizationCodeFlow),
            ["AllowClientCredentialsFlow"] = target.Flows.Contains(OpenIdToolSupport.ClientCredentialsFlow),
            ["AllowHybridFlow"] = target.Flows.Contains(OpenIdToolSupport.HybridFlow),
            ["AllowImplicitFlow"] = target.Flows.Contains(OpenIdToolSupport.ImplicitFlow),
            ["AllowPasswordFlow"] = target.Flows.Contains(OpenIdToolSupport.PasswordFlow),
            ["AllowRefreshTokenFlow"] = target.Flows.Contains(OpenIdToolSupport.RefreshTokenFlow),
            ["AllowLogoutEndpoint"] = target.Endpoints.Contains(OpenIdToolSupport.LogoutEndpoint),
            ["AllowIntrospectionEndpoint"] = target.Endpoints.Contains(OpenIdToolSupport.IntrospectionEndpoint),
            ["AllowRevocationEndpoint"] = target.Endpoints.Contains(OpenIdToolSupport.RevocationEndpoint),
            ["RequireProofKeyForCodeExchange"] = target.RequirePkce,
            ["RequirePushedAuthorizationRequests"] = target.RequirePushedAuthorizationRequests,
        };
    }

    private static JsonArray ToEntries(IEnumerable<string> names)
    {
        var entries = new JsonArray();

        foreach (var name in names)
        {
            entries.Add(new JsonObject
            {
                [EntryNameField] = name,
            });
        }

        return entries;
    }

    private static List<string> GetChanges(OpenIdApplicationState before, OpenIdApplicationState after)
    {
        var changes = new List<string>();

        void Add(string field, bool changed)
        {
            if (changed)
            {
                changes.Add(field);
            }
        }

        Add("displayName", !string.Equals(before.DisplayName, after.DisplayName, StringComparison.Ordinal));
        Add("consentType", !string.Equals(before.ConsentType, after.ConsentType, StringComparison.OrdinalIgnoreCase));
        Add("redirectUris", !SameSet(before.RedirectUris, after.RedirectUris));
        Add("postLogoutRedirectUris", !SameSet(before.PostLogoutRedirectUris, after.PostLogoutRedirectUris));
        Add("allowedFlows", !SameSet(before.Flows, after.Flows));
        Add("allowedEndpoints", !SameSet(before.Endpoints, after.Endpoints));
        Add("requirePkce", before.RequirePkce != after.RequirePkce);
        Add("requirePushedAuthorizationRequests", before.RequirePushedAuthorizationRequests != after.RequirePushedAuthorizationRequests);
        Add("roles", !SameSet(before.Roles, after.Roles));
        Add("scopes", !SameSet(before.Scopes, after.Scopes));

        return changes;
    }

    private static List<string> GetWarnings(OpenIdApplicationState state)
    {
        var warnings = new List<string>();
        var isConfidential = OpenIdToolSupport.IsConfidential(state);

        if (!isConfidential && state.Flows.Contains(OpenIdToolSupport.ClientCredentialsFlow))
        {
            warnings.Add("A public client cannot use the client credentials flow: it has no secret to sign in with.");
        }

        if (state.Roles.Count > 0 && !state.Flows.Contains(OpenIdToolSupport.ClientCredentialsFlow))
        {
            warnings.Add("The roles apply to the client credentials flow only, and that flow is not allowed for this application.");
        }

        if (!isConfidential && state.Flows.Contains(OpenIdToolSupport.AuthorizationCodeFlow) && !state.RequirePkce)
        {
            warnings.Add("A public client should be required to use PKCE with the authorization code flow.");
        }

        if (state.Flows.Contains(OpenIdToolSupport.ImplicitFlow) || state.Flows.Contains(OpenIdToolSupport.PasswordFlow))
        {
            warnings.Add("The implicit and password flows are discouraged; prefer the authorization code flow with PKCE.");
        }

        return warnings;
    }

    private static bool TryMatch(List<string> given, string[] known, out List<string> matched, out string unknown)
    {
        matched = [];
        unknown = null;

        foreach (var name in given)
        {
            var match = known.FirstOrDefault(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                unknown = name;

                return false;
            }

            if (!matched.Contains(match))
            {
                matched.Add(match);
            }
        }

        return true;
    }

    private static bool IsPublic(string type)
        => string.Equals(type?.Trim(), OpenIddictConstants.ClientTypes.Public, StringComparison.OrdinalIgnoreCase);

    private static bool IsAbsoluteHttps(string address)
    {
        // The OpenID module splits the stored list on spaces and commas.
        if (address.Contains(' ') || address.Contains(','))
        {
            return false;
        }

        return Uri.TryCreate(address, UriKind.Absolute, out var uri) &&
            uri.IsWellFormedOriginalString() &&
            uri.Scheme == Uri.UriSchemeHttps &&
            string.IsNullOrEmpty(uri.Fragment);
    }

    private static bool SameSet(List<string> first, List<string> second)
        => first.Count == second.Count && !first.Except(second, StringComparer.Ordinal).Any();

    private static string FormatLink(string url)
    {
        return string.IsNullOrEmpty(url)
            ? string.Empty
            : $" ({url})";
    }
}
