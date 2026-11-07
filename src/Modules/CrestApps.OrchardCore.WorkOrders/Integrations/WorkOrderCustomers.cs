using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.WorkOrders.Integrations;

/// <summary>
/// The customers, accounts and CRM contacts a work order can be for, found through the shared party sources (users,
/// accounts and CRM contacts). The work order's customer is a user (party kind <c>User</c>).
/// </summary>
public sealed class WorkOrderCustomers
{
    private readonly AssetOwnerDirectory _owners;
    private readonly ISession _session;
    private readonly IEnumerable<IAccountMemberService> _members;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderCustomers"/> class.
    /// </summary>
    /// <param name="parties">The party directory.</param>
    /// <param name="session">The session, for a contact's account.</param>
    /// <param name="members">The account members, when the Account Members feature is on.</param>
    public WorkOrderCustomers(IPartyDirectory parties, ISession session, IEnumerable<IAccountMemberService> members)
    {
        _owners = new AssetOwnerDirectory(parties);
        _session = session;
        _members = members;
    }

    /// <summary>
    /// Gets the account a work order is for when none is chosen: the account its contact belongs to, or else the
    /// account its customer is a member of.
    /// </summary>
    /// <param name="contactId">The CRM contact.</param>
    /// <param name="customerId">The customer, a user.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The account's content item id, or <see langword="null"/>.</returns>
    public async Task<string> AccountOfAsync(string contactId, string customerId, CancellationToken cancellationToken = default)
    {
        if (await AccountOfContactAsync(contactId, cancellationToken) is { } accountId)
        {
            return accountId;
        }

        if (string.IsNullOrEmpty(customerId))
        {
            return null;
        }

        foreach (var members in _members)
        {
            if (await members.FindAccountAsync(customerId, cancellationToken) is { } account)
            {
                return account.ContentItemId;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the address of an account's first location, kept by the Account Locations feature, or
    /// <see langword="null"/>.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The address.</returns>
    public async Task<Address> AccountAddressAsync(string accountId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(accountId))
        {
            return null;
        }

        var account = await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentItemId == accountId && index.Latest).FirstOrDefaultAsync(cancellationToken);

        if (account is null || !account.TryGet<AccountLocationsPart>(out var locations))
        {
            return null;
        }

        return locations.Locations?.FirstOrDefault(location => location.Address is not null)?.Address;
    }

    /// <summary>
    /// Gets the CRM account a contact belongs to (the list the contact is in), or <see langword="null"/>.
    /// </summary>
    /// <param name="contactId">The contact's content item.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The account's content item id.</returns>
    public async Task<string> AccountOfContactAsync(string contactId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(contactId))
        {
            return null;
        }

        var contact = await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentItemId == contactId && index.Latest).FirstOrDefaultAsync(cancellationToken);

        return AccountOf(contact);
    }

    /// <summary>
    /// Gets the CRM account a contact content item belongs to, or <see langword="null"/>. Pure.
    /// </summary>
    /// <param name="contact">The contact.</param>
    /// <returns>The account's content item id.</returns>
    public static string AccountOf(ContentItem contact)
    {
        if (contact is null)
        {
            return null;
        }

        // A contact is in its account's list, the way the Accounts module keeps an account's records.
        string accountId = contact.Content.ContainedPart?.ListContentItemId;

        return string.IsNullOrEmpty(accountId) ? null : accountId;
    }

    /// <summary>
    /// Gets the names of a customer, CRM account and CRM contact by kind (Customer, Account, Contact). With
    /// <paramref name="pickers"/>, every kind that can be picked is there, with an empty name when none is chosen.
    /// </summary>
    /// <param name="customerId">The customer.</param>
    /// <param name="accountId">The CRM account.</param>
    /// <param name="contactId">The CRM contact.</param>
    /// <param name="pickers">Whether to list every kind that can be picked.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The names by kind.</returns>
    public async Task<Dictionary<string, string>> NamesAsync(string customerId, string accountId, string contactId, bool pickers, CancellationToken cancellationToken = default)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (kind, id) in new[] { (AssetOwnerKind.Customer, customerId), (AssetOwnerKind.Account, accountId), (AssetOwnerKind.Contact, contactId) })
        {
            if (!CanPick(kind))
            {
                continue;
            }

            var name = string.IsNullOrEmpty(id) ? null : await NameAsync(kind, id, cancellationToken) ?? id;

            if (pickers || name is not null)
            {
                names[kind.ToString()] = name ?? string.Empty;
            }
        }

        return names;
    }

    /// <summary>
    /// Gets whether a kind of customer can be picked.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>Whether it can.</returns>
    public bool CanPick(AssetOwnerKind kind)
        => _owners.GetSource(kind) is not null;

    /// <summary>
    /// Gets a customer's name, or <see langword="null"/>.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The name.</returns>
    public Task<string> NameAsync(AssetOwnerKind kind, string id, CancellationToken cancellationToken = default)
        => _owners.GetNameAsync(kind, id, cancellationToken);

    /// <summary>
    /// Finds customers of a kind.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="query">The text typed.</param>
    /// <param name="count">The most to return.</param>
    /// <param name="cancellationToken">A token to cancel the search.</param>
    /// <returns>The customers found.</returns>
    public async Task<IReadOnlyList<PartyOption>> SearchAsync(AssetOwnerKind kind, string query, int count, CancellationToken cancellationToken = default)
        => _owners.GetSource(kind) is { } source ? await source.SearchAsync(query, count, cancellationToken) : [];
}

/// <summary>
/// The search behind the work order editor's customer, account and contact pickers.
/// </summary>
internal static class WorkOrderCustomerEndpoints
{
    private const int MaxResults = 50;

    /// <summary>
    /// Maps the search endpoint.
    /// </summary>
    /// <param name="builder">The endpoint route builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder AddWorkOrderCustomerEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("Admin/api/crestapps/work-orders/customers/search", (Delegate)SearchAsync)
            .RequireAuthorization()
            .WithName("CrestApps.WorkOrders.CustomerSearch");

        return builder;
    }

    internal static async Task<IResult> SearchAsync(AssetOwnerKind kind, string query, HttpContext httpContext, IAuthorizationService authorizationService, WorkOrderCustomers customers)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, WorkOrdersPermissions.ManageWorkOrders))
        {
            return Results.Forbid();
        }

        var found = await customers.SearchAsync(kind, query, MaxResults, httpContext.RequestAborted);

        return Results.Ok(found.Select(owner => new { value = owner.Id, text = owner.Name }));
    }
}
