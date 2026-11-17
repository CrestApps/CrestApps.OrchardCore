using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Security.Services;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;
using OrchardCore.Users.ViewModels;

namespace CrestApps.OrchardCore.AI.Agent.Users;

/// <summary>
/// Lists user accounts with the filters of the Users admin page. It builds the filter text a person types in that
/// page's search box and runs it through the same parser and query service, so the role restriction of that page
/// applies. It returns an explicit list of fields and never a credential.
/// </summary>
public sealed class ListUsersTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listUsers";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "text": {
          "type": "string",
          "description": "Text to find in the user name. Leave it out to list every user."
        },
        "email": {
          "type": "string",
          "description": "Text to find in the email address, for example contoso.com."
        },
        "role": {
          "type": "string",
          "description": "List the users of this role only: an exact role name from listRoles, or Authenticated for users that have no role."
        },
        "status": {
          "type": "string",
          "enum": [
            "enabled",
            "disabled"
          ],
          "description": "List only enabled users or only disabled users. A disabled user cannot sign in; new registrations wait as disabled users when the site moderates them."
        },
        "sort": {
          "type": "string",
          "enum": [
            "name",
            "email"
          ],
          "description": "The order of the list: by user name (default) or by email."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1. The default is 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of users on a page. The default is 20 and the most is 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists user accounts with optional filters (text in the user name, email, role, enabled or disabled), sorting and paging. Use it to find a user's id before any other user tool; it returns id, user name, email, roles and status only, never password or token data.";

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

        // The check of the Users admin page: the permission is checked on a blank user, so a caller who may list the
        // users of one role only gets in, and the role restriction below limits what is listed.
        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, UsersPermissions.ListUsers, new User()))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        // Each entry is one term of the filter text, with the name the parser gives it.
        var terms = new List<(string Name, string Text)>();

        if (arguments.TryGetFirstString("email", out var email) && !string.IsNullOrWhiteSpace(email))
        {
            email = email.Trim();

            if (!IsFilterValue(email) || email.Any(char.IsWhiteSpace))
            {
                return "Nothing was listed: email must be a part of an email address without spaces, quotes or colons, for example contoso.com.";
            }

            terms.Add(("email", $"email:{email}"));
        }

        if (arguments.TryGetFirstString("role", out var role) && !string.IsNullOrWhiteSpace(role))
        {
            var (roleName, error) = await ResolveRoleAsync(services, role.Trim());

            if (error is not null)
            {
                return error;
            }

            terms.Add(("role", $"role:{Quote(roleName)}"));
        }

        if (arguments.TryGetFirstString("status", out var status))
        {
            var usersFilter = status.Trim().ToUpperInvariant() switch
            {
                "ENABLED" => UsersFilter.Enabled,
                "DISABLED" => UsersFilter.Disabled,
                _ => (UsersFilter?)null,
            };

            if (usersFilter is null)
            {
                return "Nothing was listed: status must be enabled or disabled.";
            }

            terms.Add(("status", $"status:{usersFilter}"));
        }

        if (arguments.TryGetFirstString("sort", out var sort))
        {
            var usersOrder = sort.Trim().ToUpperInvariant() switch
            {
                "NAME" => UsersOrder.Name,
                "EMAIL" => UsersOrder.Email,
                _ => (UsersOrder?)null,
            };

            if (usersOrder is null)
            {
                return "Nothing was listed: sort must be name or email.";
            }

            terms.Add(("sort", $"sort:{usersOrder}"));
        }

        // The text goes last: the parser stops at what it cannot read, and the filters before it must still apply.
        if (arguments.TryGetFirstString("text", out var text) && !string.IsNullOrWhiteSpace(text))
        {
            text = text.Trim();

            if (!IsFilterValue(text))
            {
                return "Nothing was listed: text must be a part of a user name without quotes or colons.";
            }

            terms.Add((UsersAdminListFilterOptions.DefaultTermName, Quote(text)));
        }

        var filterText = string.Join(' ', terms.Select(term => term.Text));
        var options = new UserIndexOptions
        {
            FilterResult = services.GetRequiredService<IUsersAdminListFilterParser>().Parse(filterText),
        };

        // A term the parser did not keep would be a filter that silently does not apply.
        if (terms.Any(term => !options.FilterResult.Any(node => string.Equals(node.TermName, term.Name, StringComparison.OrdinalIgnoreCase))))
        {
            return "Nothing was listed: the filter could not be read. Use plain words in text and email.";
        }

        options.FilterResult.MapTo(options);

        var query = await services.GetRequiredService<IUsersAdminListQueryService>().QueryAsync(options, new NullModelUpdater());
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);
        var total = await query.CountAsync(cancellationToken);
        var users = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ListAsync(cancellationToken);

        var items = new List<object>();
        var notListed = 0;

        foreach (var user in users)
        {
            // The list of the admin page shows the users of every role to a caller who may list one role. The tool
            // returns personal data to a model, so it keeps to the users the caller may list.
            if (!await AgentToolAuthorization.IsAuthorizedAsync(services, UsersPermissions.ListUsers, user))
            {
                notListed++;

                continue;
            }

            items.Add(UserToolProjection.Summarize(user));
        }

        return AgentToolResults.Json(new
        {
            items,
            total,
            page,
            pageSize,
            truncated = total > page * pageSize
                ? true
                : (bool?)null,
            filter = filterText,
            notListed = notListed > 0
                ? notListed
                : (int?)null,
            note = notListed > 0
                ? "Some users on this page are not listed because you may not list the users of their roles."
                : null,
        });
    }

    private static async Task<(string RoleName, string Error)> ResolveRoleAsync(IServiceProvider services, string role)
    {
        if (!IsFilterValue(role))
        {
            return (null, "Nothing was listed: role must be the exact name of a role, from listRoles.");
        }

        // The Users admin page offers this choice for the users that have no role.
        if (string.Equals(role, OrchardCoreConstants.Roles.Authenticated, StringComparison.OrdinalIgnoreCase))
        {
            return (OrchardCoreConstants.Roles.Authenticated, null);
        }

        var roleNames = await services.GetRequiredService<IRoleService>().GetRoleNamesAsync();
        var roleName = roleNames.FirstOrDefault(name => string.Equals(name, role, StringComparison.OrdinalIgnoreCase));

        if (roleName is null)
        {
            return (null, $"Nothing was listed: there is no role named '{role}'. Use listRoles to get the exact role names.");
        }

        // The role filter of the admin page offers the roles whose users the caller may list.
        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, UsersPermissions.CreateListUsersInRolePermission(roleName)))
        {
            return (null, $"Nothing was listed: you are not allowed to list the users of the role '{roleName}'.");
        }

        return (roleName, null);
    }

    private static bool IsFilterValue(string value)
        => !value.Any(character => character is '"' or '\'' or ':');

    private static string Quote(string value)
        => value.Any(char.IsWhiteSpace)
            ? $"\"{value}\""
            : value;
}
