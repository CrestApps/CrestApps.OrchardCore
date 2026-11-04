using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;

namespace CrestApps.OrchardCore.Accounts.Handlers;

/// <summary>
/// Links each member to the account's contact with the member's email, when exactly one contact has it, each time the
/// account is saved. A member whose contact left the account, or whose email changed, loses the link. The contacts are
/// found by the registered <see cref="IAccountContactFinder"/>s; with none, no member is linked.
/// </summary>
internal sealed class AccountMembersPartHandler : ContentPartHandler<AccountMembersPart>
{
    private readonly IEnumerable<IAccountContactFinder> _contactFinders;

    public AccountMembersPartHandler(IEnumerable<IAccountContactFinder> contactFinders)
    {
        _contactFinders = contactFinders;
    }

    public override async Task UpdatingAsync(UpdateContentContext context, AccountMembersPart part)
    {
        if (part.Members is not { Count: > 0 } members)
        {
            return;
        }

        var accountId = context.ContentItem.ContentItemId;

        foreach (var member in members.Where(member => member is not null))
        {
            member.ContactContentItemId = await FindContactAsync(accountId, member.Email);
        }

        context.ContentItem.Apply(part);
    }

    private async Task<string> FindContactAsync(string accountId, string email)
    {
        foreach (var finder in _contactFinders)
        {
            if (await finder.FindContactByEmailAsync(accountId, email) is { Length: > 0 } contactId)
            {
                return contactId;
            }
        }

        return null;
    }
}
