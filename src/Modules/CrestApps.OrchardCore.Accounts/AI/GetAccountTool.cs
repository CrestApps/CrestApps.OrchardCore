using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.Accounts.AI;

/// <summary>
/// Reads one account as its admin page shows it: the latest version, for a caller who may open the admin panel and list
/// content items (<c>AccessAdminPanel</c> and <c>ListContent</c>, as the admin's content list and <c>listAccounts</c>
/// need) and holds <c>ViewContent</c> on the account, as the page's Display action checks. It returns an explicit list of fields: the account's name, type and state, and the
/// sections its type has, each as the account page's Overview tab shows it: the members (who signs in for the account and
/// with what role), the locations with their postal address and which one is billed, the owner and account team with
/// their staff names, and the e-mail domains whose people belong to the account. The content item itself is never serialized, unlike <c>getContentItemById</c>.
/// </summary>
internal sealed class GetAccountTool : AccountsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getAccount";

    // The route of an item's page in the admin: the Display action of OrchardCore.Contents.
    private const string AdminDisplayRouteName = "AdminContentItem";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "contentItemId": {
          "type": "string",
          "description": "The account's id, from listAccounts."
        }
      },
      "required": ["contentItemId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns one account (a business or household) by its id from listAccounts: name, type, whether it is published or has unpublished changes, when it was created and changed, and, when its type has them, its members (the users who sign in for it, with their role), its locations with their addresses (and which one is the billing location), its owner and account team, and the e-mail domains whose people belong to it. Use it to answer questions about an account and before changing its members, locations or team; it reads what the account's page shows, not the raw content item.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var contentItemId = ReadString(arguments, "contentItemId");

        if (contentItemId is null)
        {
            return "Nothing was done: give the account's contentItemId. Use listAccounts to find it.";
        }

        // As the admin's account pages: the admin panel and the content list first, so an account's existence is not
        // told to a caller who could not reach them.
        if (!await IsAuthorizedAsync(services, caller, AdminPermissions.AccessAdminPanel) ||
            !await IsAuthorizedAsync(services, caller, CommonPermissions.ListContent))
        {
            return NotAllowedMessage;
        }

        var (account, error) = await LoadAccountAsync(services, contentItemId, partName: null);

        if (account is null)
        {
            return error;
        }

        // As the account's page: the latest version, for a caller who may view the account.
        if (!await IsAuthorizedAsync(services, caller, CommonPermissions.ViewContent, account))
        {
            return NotAllowedMessage;
        }

        var contentManager = services.GetRequiredService<IContentManager>();
        var published = account.Published ? account : await contentManager.GetAsync(account.ContentItemId, VersionOptions.Published);
        var hasUnpublishedChanges = published is not null && !string.Equals(published.ContentItemVersionId, account.ContentItemVersionId, StringComparison.Ordinal);
        var definition = await services.GetRequiredService<IContentDefinitionManager>().GetTypeDefinitionAsync(account.ContentType);

        object members = null;
        object locations = null;
        object team = null;
        object emailDomains = null;

        if (AccountRecordKinds.HasPart(definition, AccountsConstants.ContentParts.Members))
        {
            members = (GetPart<AccountMembersPart>(account)?.Members ?? [])
                .Where(member => member is not null)
                .Select(member => new
                {
                    // As the page shows a member: by email, or by user name when there is none.
                    user = member.Email ?? member.UserName,
                    role = member.Role.ToString(),
                    contactContentItemId = string.IsNullOrEmpty(member.ContactContentItemId) ? null : member.ContactContentItemId,
                })
                .ToList();
        }

        if (AccountRecordKinds.HasPart(definition, AccountsConstants.ContentParts.Locations))
        {
            locations = (GetPart<AccountLocationsPart>(account)?.Locations ?? [])
                .Where(location => location is not null)
                .Select(location => new
                {
                    locationId = location.LocationId,
                    name = location.Name,
                    address = AddressArguments.Describe(location.Address),
                    isBilling = location.IsBilling,
                })
                .ToList();
        }

        if (AccountRecordKinds.HasPart(definition, AccountsConstants.ContentParts.Team))
        {
            team = await DescribeTeamAsync(services, GetPart<AccountTeamPart>(account), cancellationToken);
        }

        // As the editor's Email domains card: the domains whose people belong to the account, as stored (normalized).
        if (AccountRecordKinds.HasPart(definition, AccountsConstants.ContentParts.EmailDomains))
        {
            emailDomains = (GetPart<AccountEmailDomainsPart>(account)?.Domains ?? [])
                .Where(domain => !string.IsNullOrWhiteSpace(domain))
                .ToList();
        }

        return Json(new
        {
            contentItemId = account.ContentItemId,
            name = account.DisplayText,
            type = account.ContentType,
            published = published is not null,
            hasUnpublishedChanges,
            createdAt = await LocalTimeAsync(services, account.CreatedUtc),
            modifiedAt = await LocalTimeAsync(services, account.ModifiedUtc),
            publishedAt = published is null ? null : await LocalTimeAsync(services, published.PublishedUtc),
            members,
            locations,
            team,
            emailDomains,
            adminPage = GetAdminPath(services, account.ContentItemId),
            note = hasUnpublishedChanges
                ? "These are the account's unpublished changes; the published account keeps its earlier values until the draft is published."
                : null,
        });
    }

    // As the page's team card: the owner and the other staff, named from the staff directory. A staff member the
    // directory no longer knows keeps its id, with no name.
    private static async Task<object> DescribeTeamAsync(IServiceProvider services, AccountTeamPart part, CancellationToken cancellationToken)
    {
        var ownerId = string.IsNullOrEmpty(part?.OwnerStaffId) ? null : part.OwnerStaffId;
        var teamIds = (part?.TeamStaffIds ?? [])
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        List<string> ids = ownerId is null ? teamIds : [.. teamIds, ownerId];
        IReadOnlyDictionary<string, string> names = ids.Count > 0 && services.GetService<IStaffDirectory>() is { } directory
            ? await directory.GetNamesAsync(ids, cancellationToken)
            : new Dictionary<string, string>();

        return new
        {
            owner = ownerId is null
                ? null
                : new
                {
                    staffId = ownerId,
                    name = names.GetValueOrDefault(ownerId),
                },
            members = teamIds.Select(id => new
            {
                staffId = id,
                name = names.GetValueOrDefault(id),
            }).ToList(),
        };
    }

    private static string GetAdminPath(IServiceProvider services, string contentItemId)
    {
        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;
        var linkGenerator = services.GetService<LinkGenerator>();

        if (httpContext is null || linkGenerator is null)
        {
            return null;
        }

        try
        {
            return linkGenerator.GetPathByRouteValues(httpContext, AdminDisplayRouteName, new RouteValueDictionary
            {
                ["area"] = OrchardCoreFeatureIds.Contents,
                ["contentItemId"] = contentItemId,
            });
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
