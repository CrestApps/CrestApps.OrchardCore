using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Accounts.AI;

/// <summary>
/// Replaces the users who sign in for an account with the members editor's rules (<see cref="AccountMemberRules"/>):
/// each user must exist and may belong to one account only, and nothing changes when one does not qualify. Saving an
/// account through the content tools skips those rules, which live in the members editor. It needs
/// <c>ManageAccountMembers</c>, which the members editor needs, and <c>EditContent</c> on the account.
/// </summary>
internal sealed class SetAccountMembersTool : AccountsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setAccountMembers";

    private static readonly JsonElement _jsonSchema = Schema(
    $$"""
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The account's id, from listAccounts."
        },
        "members": {
          "type": "array",
          "description": "Every user who signs in for the account, replacing the current members. An empty list removes them all.",
          "items": {
            "type": "object",
            "properties": {
              "user": {
                "type": "string",
                "description": "The user's email or user name."
              },
              "role": {
                "type": "string",
                "enum": ["Member", "Admin"],
                "description": "Member acts for the account in what they do themselves; Admin also manages the account for the other members, such as seeing and approving all its orders. Member by default."
              }
            },
            "required": ["user"],
            "additionalProperties": false
          }
        },
        "saveAsDraft": {
          "type": "boolean",
          "description": "{{SaveAsDraftDescription}}"
        }
      },
      "required": ["contentItemId", "members"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Replaces the users who sign in for an account and their role (Member or Admin), by user name or email. Each user must exist and can belong to one account only; the tool fails without changing anything when one does not qualify.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, caller, AccountsPermissions.ManageAccountMembers))
        {
            return NotAllowedMessage;
        }

        var (account, error) = await LoadAccountAsync(services, ReadString(arguments, "contentItemId"), AccountsConstants.ContentParts.Members);

        if (account is null)
        {
            return error;
        }

        if (!await IsAuthorizedAsync(services, caller, CommonPermissions.EditContent, account))
        {
            return NotAllowedMessage;
        }

        if (ReadJson(arguments, "members") is not { ValueKind: JsonValueKind.Array } membersJson)
        {
            return "Nothing was changed: give every member in members, as a list of { user, role }, or an empty list to remove them all.";
        }

        var rows = new List<(string User, AccountMemberRole Role)>();

        foreach (var row in membersJson.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty("user", out var userJson) || userJson.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(userJson.GetString()))
            {
                return "Nothing was changed: each member needs a user, the user's email or user name.";
            }

            if (row.EnumerateObject().Select(property => property.Name).FirstOrDefault(name => name is not ("user" or "role")) is { } unknown)
            {
                return $"Nothing was changed: '{unknown}' is not a field of a member. Give user and role.";
            }

            var role = AccountMemberRole.Member;

            if (row.TryGetProperty("role", out var roleJson) && roleJson.ValueKind != JsonValueKind.Null &&
                (roleJson.ValueKind != JsonValueKind.String || !Enum.TryParse(roleJson.GetString(), ignoreCase: true, out role) || !Enum.IsDefined(role)))
            {
                return $"Nothing was changed: '{roleJson}' is not a role. Use Member or Admin.";
            }

            rows.Add((userJson.GetString().Trim(), role));
        }

        var part = GetPart<AccountMembersPart>(account) ?? new AccountMembersPart();

        part.ContentItem = account;

        var resolution = await AccountMemberRules.ResolveAsync(
            services.GetRequiredService<UserManager<IUser>>(),
            services.GetRequiredService<IAccountMemberService>(),
            services.GetRequiredService<IContentManager>(),
            part,
            rows);

        if (resolution.Problems.Count > 0)
        {
            return $"Nothing was changed: {string.Join(" ", resolution.Problems.Select(problem => problem.Kind == AccountMemberProblemKind.UnknownUser
                ? $"There's no user {problem.User}; add them as a user first."
                : $"{problem.User} is already a member of {problem.OtherAccountName}; a user can belong to one account only."))}";
        }

        var previous = (part.Members ?? []).Select(member => member.UserId).ToHashSet(StringComparer.Ordinal);
        var (item, published, saveError) = await SaveAccountAsync(services, caller, account, ReadBool(arguments, "saveAsDraft") == true, version =>
            version.Alter<AccountMembersPart>(stored => stored.Members = resolution.Members));

        if (item is null)
        {
            return saveError;
        }

        return Json(new
        {
            contentItemId = item.ContentItemId,
            name = item.DisplayText,
            members = resolution.Members.Select(member => new
            {
                userId = member.UserId,
                userName = member.UserName,
                email = member.Email,
                role = member.Role.ToString(),
            }).ToList(),
            added = resolution.Members.Count(member => !previous.Contains(member.UserId)),
            removed = previous.Count(userId => !resolution.Members.Any(member => member.UserId == userId)),
            published,
            savedAsDraft = !published,
            message = published
                ? "The members were saved and the account was published. These users now sign in for the account."
                : "The members were saved in a draft. The published account, if any, keeps its current members until the draft is published.",
        });
    }
}
