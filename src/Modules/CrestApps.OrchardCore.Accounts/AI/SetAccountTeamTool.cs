using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.Accounts.AI;

/// <summary>
/// Sets the staff who look after an account as the account team editor does: the owner, and the rest of the team without
/// the owner and without repeats. Stricter than the editor, whose pickers only offer staff: every id must be a staff
/// member. It needs <c>EditContent</c> on the account.
/// </summary>
internal sealed class SetAccountTeamTool : AccountsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setAccountTeam";

    private static readonly JsonElement _jsonSchema = Schema(
    $$"""
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The account's id, from listAccounts."
        },
        "ownerStaffId": {
          "type": "string",
          "description": "The staff member id of the account's owner (its account manager or sales rep). An empty text removes the owner; leave it out to keep the owner."
        },
        "teamStaffIds": {
          "type": "array",
          "items": { "type": "string" },
          "description": "The staff member ids of the rest of the account team, replacing the current team. An empty list removes them; leave it out to keep the team."
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
    public override string Description => "Sets the staff member who owns an account and the other staff on its account team, by staff member id. Find the ids with the staff search tool first.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (account, error) = await LoadAccountAsync(services, ReadString(arguments, "contentItemId"), AccountsConstants.ContentParts.Team);

        if (account is null)
        {
            return error;
        }

        if (!await IsAuthorizedAsync(services, caller, CommonPermissions.EditContent, account))
        {
            return NotAllowedMessage;
        }

        if (!Has(arguments, "ownerStaffId") && !Has(arguments, "teamStaffIds"))
        {
            return "Nothing was changed: give the owner in ownerStaffId, the team in teamStaffIds, or both.";
        }

        var current = GetPart<AccountTeamPart>(account);
        var ownerStaffId = Has(arguments, "ownerStaffId") ? ReadString(arguments, "ownerStaffId") : current?.OwnerStaffId;
        var teamStaffIds = (Has(arguments, "teamStaffIds") ? ReadStringList(arguments, "teamStaffIds") : current?.TeamStaffIds?.ToList()) ?? [];

        teamStaffIds = [.. teamStaffIds.Where(id => id != ownerStaffId).Distinct(StringComparer.Ordinal)];

        var directory = services.GetService<IStaffDirectory>();

        if (directory is null)
        {
            return "Nothing was changed: the Staff feature is not on, so staff members cannot be found.";
        }

        var given = new List<string>();

        if (Has(arguments, "ownerStaffId") && ownerStaffId is not null)
        {
            given.Add(ownerStaffId);
        }

        if (Has(arguments, "teamStaffIds"))
        {
            given.AddRange(teamStaffIds);
        }

        var found = given.Count == 0 ? new Dictionary<string, string>() : await directory.GetNamesAsync(given, cancellationToken);

        if (given.FirstOrDefault(id => !found.ContainsKey(id)) is { } unknown)
        {
            return $"Nothing was changed: '{unknown}' is not a staff member id. Find the ids with the staff search tool.";
        }

        var (item, published, saveError) = await SaveAccountAsync(services, caller, account, ReadBool(arguments, "saveAsDraft") == true, version =>
            version.Alter<AccountTeamPart>(part =>
            {
                part.OwnerStaffId = ownerStaffId;
                part.TeamStaffIds = teamStaffIds;
            }));

        if (item is null)
        {
            return saveError;
        }

        var names = await directory.GetNamesAsync(teamStaffIds.Append(ownerStaffId).Where(id => id is not null), cancellationToken);

        return Json(new
        {
            contentItemId = item.ContentItemId,
            account = item.DisplayText,
            owner = ownerStaffId is null ? null : new { staffId = ownerStaffId, name = names.GetValueOrDefault(ownerStaffId) },
            team = teamStaffIds.Select(id => new { staffId = id, name = names.GetValueOrDefault(id) }).ToList(),
            published,
            savedAsDraft = !published,
            message = published
                ? "The account team was saved and the account was published."
                : "The account team was saved in a draft. The published account, if any, keeps its current team until the draft is published.",
        });
    }
}
