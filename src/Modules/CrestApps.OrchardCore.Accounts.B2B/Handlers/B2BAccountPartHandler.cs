using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.Services;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;

namespace CrestApps.OrchardCore.Accounts.B2B.Handlers;

/// <summary>
/// Links each buyer to the account's contact with the buyer's email, when exactly one contact has it, each time the
/// account is saved. A buyer whose contact left the account, or whose email changed, loses the link.
/// </summary>
internal sealed class B2BAccountPartHandler : ContentPartHandler<B2BAccountPart>
{
    private readonly IEnumerable<IAccountContactFinder> _contactFinders;

    public B2BAccountPartHandler(IEnumerable<IAccountContactFinder> contactFinders)
    {
        _contactFinders = contactFinders;
    }

    public override async Task UpdatingAsync(UpdateContentContext context, B2BAccountPart part)
    {
        if (part.Buyers is not { Count: > 0 } buyers)
        {
            return;
        }

        var accountId = context.ContentItem.ContentItemId;

        foreach (var buyer in buyers)
        {
            buyer.ContactContentItemId = await FindContactAsync(accountId, buyer.Email);
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
