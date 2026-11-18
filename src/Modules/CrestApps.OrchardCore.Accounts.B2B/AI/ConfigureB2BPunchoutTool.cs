using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Accounts.B2B.AI;

/// <summary>
/// Turns punchout on or off for an account and chooses the member its procurement system shops as, with the Business
/// buying card's rules (<see cref="B2BAccountSettingsRules"/>): the buyer is one of the account's members and never a
/// site administrator. A punchout key is a credential: this tool never issues, returns or accepts one. Issuing the
/// first key, or a new one, is done on the account's editor, which shows the key once; punchout cannot be turned on here
/// for an account that has no key yet. It needs <c>ManageB2BAccounts</c> and <c>EditContent</c> on the account, and asks
/// for approval.
/// </summary>
internal sealed class ConfigureB2BPunchoutTool : B2BAccountsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "configureB2BPunchout";

    private static readonly JsonElement _jsonSchema = Schema(
    $$"""
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The account's id, from listAccounts."
        },
        "enabled": {
          "type": "boolean",
          "description": "Whether the account's procurement system may open the store by OCI punchout. Turning it off also clears the punchout buyer; the key is kept. Leave it out to keep the current state."
        },
        "buyer": {
          "type": "string",
          "description": "The user name or email of the account member the procurement system shops as. Must be one of the account's members and not a site administrator. Needed to turn punchout on when no buyer is chosen yet."
        },
        "saveAsDraft": {
          "type": "boolean",
          "description": "{{SaveAsDraftDescription}}"
        }
      },
      "required": ["contentItemId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Enables or disables punchout for an account and chooses the member the procurement system shops as; the buyer cannot be a site administrator. Never issues, returns or accepts a punchout key: a new key is issued on the account's editor, which shows it once, and the result gives that page's address.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (account, error) = await LoadAccountAsync(services, ReadString(arguments, "contentItemId"));

        if (account is null)
        {
            return error;
        }

        if (!await IsAuthorizedAsync(services, caller, CommonPermissions.EditContent, account))
        {
            return NotAllowedMessage;
        }

        if (!Has(arguments, "enabled") && !Has(arguments, "buyer"))
        {
            return "Nothing was changed: give enabled, buyer or both.";
        }

        var stored = account.TryGet<B2BAccountPart>(out var part) ? part.Punchout : null;
        var enabled = Has(arguments, "enabled") ? ReadBool(arguments, "enabled") ?? stored?.Enabled == true : stored?.Enabled == true;
        var editorPath = GetAccountEditorPath(services, account.ContentItemId);
        var members = GetMembers(account);
        string buyerId = null;

        if (enabled)
        {
            if (string.IsNullOrEmpty(stored?.KeyHash))
            {
                return Json(new
                {
                    changed = false,
                    message = "Nothing was changed: the account has no punchout key yet. Turn punchout on in the Business buying card of the account's editor, which issues the key and shows it once to copy into the procurement system. Keys are never issued or shown in a chat.",
                    editorPath,
                });
            }

            var buyer = ReadString(arguments, "buyer");

            if (buyer is not null)
            {
                if (FindMember(members, buyer) is not { } member)
                {
                    return $"Nothing was changed: {buyer} is not a member of the account. Choose one of its members, or add them with setAccountMembers first.";
                }

                buyerId = member.UserId;
            }
            else
            {
                buyerId = stored?.UserId;
            }

            switch (await B2BAccountSettingsRules.CheckPunchoutUserAsync(buyerId, members, services.GetRequiredService<UserManager<IUser>>()))
            {
                case B2BAccountSettingsProblem.PunchoutUserNotMember:
                    return "Nothing was changed: choose one of the account's members as the punchout buyer, in buyer.";
                case B2BAccountSettingsProblem.PunchoutUserIsAdministrator:
                    return "Nothing was changed: a site administrator can't be the punchout buyer. Use a user who only buys.";
            }
        }
        else if (ReadString(arguments, "buyer") is not null)
        {
            return "Nothing was changed: punchout is off, and the buyer is chosen only while it is on. Pass enabled: true with the buyer.";
        }

        var (item, published, saveError) = await SaveAccountAsync(services, caller, account, ReadBool(arguments, "saveAsDraft") == true, version =>
            version.Alter<B2BAccountPart>(saved => B2BAccountSettingsRules.ApplyPunchout(saved, enabled, buyerId)));

        if (item is null)
        {
            return saveError;
        }

        var saved = item.TryGet<B2BAccountPart>(out var savedPart) ? savedPart.Punchout : null;
        var chosen = buyerId is null ? null : members.FindMember(buyerId);

        return Json(new
        {
            changed = true,
            contentItemId = item.ContentItemId,
            account = item.DisplayText,
            enabled,
            buyer = chosen is null ? null : new { userId = chosen.UserId, userName = chosen.UserName, email = chosen.Email },
            hasKey = !string.IsNullOrEmpty(saved?.KeyHash),
            keyCreated = await LocalTimeAsync(services, saved?.KeyCreatedUtc),
            published,
            savedAsDraft = !published,
            message = (enabled ? "Punchout is on" : "Punchout is off")
                + (published ? " and the account was published." : " in a draft. The published account, if any, keeps its current punchout until the draft is published.")
                + " A new key is issued only on the account's editor, which shows it once.",
            editorPath,
        });
    }
}
