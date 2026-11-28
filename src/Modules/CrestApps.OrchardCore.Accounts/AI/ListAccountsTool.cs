using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Contents;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Accounts.AI;

/// <summary>
/// Finds accounts by name as the account search behind the account pickers does: it needs <c>ListContent</c>, and only
/// the account types the caller may view (<c>ViewContent</c> per type) are searched. Unlike the picker, which returns the
/// first 50 names, it pages and returns each account's type, state, member count, locations and owner.
/// </summary>
internal sealed class ListAccountsTool : AccountsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listAccounts";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "Text in the account's name, such as 'Northwind'. Leave it out to list every account."
        },
        "page": {
          "type": "integer",
          "description": "The page, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "The accounts per page, 20 by default and at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Finds accounts (businesses and households) by name across every account content type and returns id, name, type, status, member count, locations and owner. Use it to get the account id; use getAccount for the account's members, locations and team.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, caller, CommonPermissions.ListContent))
        {
            return NotAllowedMessage;
        }

        var accountTypes = new List<string>();

        foreach (var type in await services.GetRequiredService<IAccountContentTypeProvider>().GetAccountTypesAsync())
        {
            if (await IsAuthorizedAsync(services, caller, CommonPermissions.ViewContent, new ContentItem { ContentType = type.Name }))
            {
                accountTypes.Add(type.Name);
            }
        }

        var page = Math.Max(1, ReadInt(arguments, "page") ?? 1);
        var pageSize = Math.Clamp(ReadInt(arguments, "pageSize") ?? DefaultPageSize, 1, MaxPageSize);

        if (accountTypes.Count == 0)
        {
            return Json(new
            {
                items = Array.Empty<object>(),
                total = 0,
                page,
                pageSize,
                truncated = false,
                message = "There is no account type you may view.",
            });
        }

        var search = ReadString(arguments, "name");
        var session = services.GetRequiredService<ISession>();
        var query = search is null
            ? session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType.IsIn(accountTypes))
            : session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType.IsIn(accountTypes) && index.DisplayText.Contains(search));

        var total = await query.CountAsync(cancellationToken);
        var accounts = await query
            .OrderBy(index => index.DisplayText)
            .ThenBy(index => index.ContentItemId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ListAsync(cancellationToken);

        var ownerIds = accounts
            .Select(account => GetPart<AccountTeamPart>(account)?.OwnerStaffId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var ownerNames = ownerIds.Count > 0 && services.GetService<IStaffDirectory>() is { } directory
            ? await directory.GetNamesAsync(ownerIds, cancellationToken)
            : new Dictionary<string, string>();

        var items = accounts.Select(account =>
        {
            var members = GetPart<AccountMembersPart>(account)?.Members;
            var locations = GetPart<AccountLocationsPart>(account)?.Locations;
            var ownerId = GetPart<AccountTeamPart>(account)?.OwnerStaffId;

            return new
            {
                id = account.ContentItemId,
                name = account.DisplayText,
                type = account.ContentType,
                status = account.Published ? "Published" : "Draft",
                memberCount = members?.Count,
                locations = locations?.Where(location => location is not null).Select(location => new { id = location.LocationId, name = location.Name }).ToList(),
                ownerStaffId = string.IsNullOrEmpty(ownerId) ? null : ownerId,
                ownerName = string.IsNullOrEmpty(ownerId) ? null : ownerNames.GetValueOrDefault(ownerId),
            };
        }).ToList();

        return Json(new
        {
            items,
            total,
            page,
            pageSize,
            truncated = page * pageSize < total,
        });
    }
}
