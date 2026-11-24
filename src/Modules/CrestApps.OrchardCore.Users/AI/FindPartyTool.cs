using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Users.AI;

/// <summary>
/// Finds people and organizations of every party kind (users, customers, accounts, contacts, staff) by name or email
/// through <see cref="IPartyDirectory.SearchAsync"/>, the directory the owner pickers of other modules use. The party
/// sources do not check permissions (each picker checks its own page's permission), so the tool searches only the kinds
/// the caller may see: users need <c>SearchUsers</c> (the user picker's permission), customers <c>ViewCustomers</c>,
/// staff <c>ViewStaff</c> (the Customers and Staff pages' read permissions, mirrored by name since those modules' types
/// cannot be referenced here), and accounts and contacts, which are content items, <c>ViewContent</c> on each item found.
/// Kinds this tool does not know are not searched.
/// </summary>
internal sealed class FindPartyTool : UsersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "findParty";

    /// <summary>
    /// The staff party kind (<c>StaffConstants.PartyKind</c> in the Staff abstractions).
    /// </summary>
    public const string StaffKind = "Staff";

    /// <summary>
    /// How many parties of each kind are returned when no limit is given.
    /// </summary>
    public const int DefaultLimit = 10;

    /// <summary>
    /// The most parties of each kind a call returns.
    /// </summary>
    public const int MaxLimit = 25;

    // The Customers and Staff pages' read permissions, by name: a permission is granted by its name, and their modules'
    // assemblies cannot be referenced from this one. The implied permissions are mirrored too.
    internal static readonly Permission ViewCustomers = new(
        "ViewCustomers",
        LocalizationSource.Create<FindPartyTool>("View customers"),
        [new Permission("ManageCustomers")]);

    internal static readonly Permission ViewStaff = new(
        "ViewStaff",
        LocalizationSource.Create<FindPartyTool>("View staff"),
        [new Permission("ManageStaff"), new Permission("ManageOwnStaff")]);

    private static readonly string[] _knownKinds = [PartyKinds.User, PartyKinds.Customer, PartyKinds.Account, PartyKinds.Contact, StaffKind];

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "query": {
          "type": "string",
          "description": "The name, or part of it, to look for; users and customers also match on email."
        },
        "kind": {
          "type": "string",
          "enum": ["User", "Customer", "Account", "Contact", "Staff"],
          "description": "Only this kind of party: User (a site user), Customer (a store customer), Account (a business account), Contact (a CRM contact) or Staff (a person who works for the business). Leave it out to search every kind you may see."
        },
        "limit": {
          "type": "integer",
          "minimum": 1,
          "maximum": 25,
          "description": "The most parties of each kind to return, from 1 to 25. Defaults to 10."
        }
      },
      "required": ["query"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Finds people and organizations by name across every party kind (users, customers, business accounts, CRM contacts, staff) in one search, and says which kind each one is. Use it first when the user names someone (\"find Northwind\") and the kind is not clear.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var query = ReadString(arguments, "query");

        if (query is null)
        {
            return "Nothing was searched: give the name to look for in query.";
        }

        var kind = ReadString(arguments, "kind");
        string requested = null;

        if (kind is not null)
        {
            requested = _knownKinds.FirstOrDefault(known => string.Equals(known, kind, StringComparison.OrdinalIgnoreCase));

            if (requested is null)
            {
                return $"Nothing was searched: kind must be one of {string.Join(", ", _knownKinds)}, not '{kind}'.";
            }
        }

        var limit = Math.Clamp(ReadWholeNumber(arguments, "limit") ?? DefaultLimit, 1, MaxLimit);
        var directory = services.GetService<IPartyDirectory>();
        var available = directory?.Kinds ?? [];
        var kinds = requested is not null ? new[] { requested } : _knownKinds;
        var items = new List<object>();
        var searched = new List<string>();
        var notAllowed = new List<string>();
        var notAvailable = new List<string>();
        var truncated = false;

        foreach (var partyKind in kinds)
        {
            if (directory is null || !available.Contains(partyKind, StringComparer.Ordinal))
            {
                notAvailable.Add(partyKind);

                continue;
            }

            var isContent = partyKind is PartyKinds.Account or PartyKinds.Contact;

            if (!isContent && !await IsAuthorizedAsync(services, caller, PermissionOf(partyKind)))
            {
                notAllowed.Add(partyKind);

                continue;
            }

            searched.Add(partyKind);

            var found = await directory.SearchAsync(partyKind, query, limit + 1, cancellationToken);

            if (found.Count > limit)
            {
                truncated = true;
                found = [.. found.Take(limit)];
            }

            if (isContent)
            {
                found = await FilterViewableAsync(services, caller, found);
            }

            items.AddRange(found.Select(party => new
            {
                kind = partyKind,
                id = party.Id,
                name = party.Name,
                party = new PartyRef(partyKind, party.Id).ToString(),
            }));
        }

        if (searched.Count == 0)
        {
            return notAllowed.Count > 0
                ? $"Nothing was searched: you are not allowed to see {string.Join(", ", notAllowed)} records."
                : requested is not null
                    ? $"Nothing was searched: no enabled feature provides {requested} records on this site."
                    : "Nothing was searched: no enabled feature provides people or organizations on this site.";
        }

        return Json(new
        {
            query,
            items,
            total = items.Count,
            truncated,
            searchedKinds = searched,
            notAllowedKinds = notAllowed.Count > 0 ? notAllowed : null,
            notAvailableKinds = requested is not null && notAvailable.Count > 0 ? notAvailable : null,
            message = items.Count == 0
                ? $"Nobody matching '{query}' was found among the {string.Join(", ", searched)} records you may see."
                : truncated
                    ? $"Only the first {limit} matches of a kind are shown; give more of the name to narrow it."
                    : null,
        });
    }

    private static Permission PermissionOf(string kind)
        => kind switch
        {
            PartyKinds.User => UserSearchPermissions.SearchUsers,
            PartyKinds.Customer => ViewCustomers,
            _ => ViewStaff,
        };

    private static async Task<IReadOnlyList<PartyOption>> FilterViewableAsync(IServiceProvider services, ClaimsPrincipal caller, IReadOnlyList<PartyOption> found)
    {
        if (found.Count == 0)
        {
            return found;
        }

        var contentItems = await services.GetRequiredService<IContentManager>().GetAsync(found.Select(party => party.Id), VersionOptions.Latest);
        var viewable = new HashSet<string>(StringComparer.Ordinal);

        foreach (var contentItem in contentItems)
        {
            if (await IsAuthorizedAsync(services, caller, CommonPermissions.ViewContent, contentItem))
            {
                viewable.Add(contentItem.ContentItemId);
            }
        }

        return [.. found.Where(party => viewable.Contains(party.Id))];
    }

    private static int? ReadWholeNumber(AIFunctionArguments arguments, string name)
        => int.TryParse(ReadString(arguments, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
}
