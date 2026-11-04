using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Commerce.B2B;

/// <summary>
/// Builds account content items that buy as a business, for the tests: the B2B part, and the account's members and
/// locations when given.
/// </summary>
internal static class TestB2BAccounts
{
    public static ContentItem CreateItem(
        string id = "account-1",
        string name = "Fabrikam Ltd",
        Action<B2BAccountPart> configure = null,
        bool published = true,
        DateTime? createdUtc = null,
        AccountMember[] members = null,
        AccountLocation[] locations = null)
    {
        var contentItem = new ContentItem
        {
            ContentItemId = id,
            ContentItemVersionId = id + "-v1",
            ContentType = "Account",
            DisplayText = name,
            Published = published,
            Latest = true,
            CreatedUtc = createdUtc,
        };

        contentItem.Weld<AccountPart>();

        if (members is not null)
        {
            contentItem.Apply(new AccountMembersPart { Members = [.. members] });
        }

        if (locations is not null)
        {
            contentItem.Apply(new AccountLocationsPart { Locations = [.. locations] });
        }

        var part = new B2BAccountPart { Enabled = true };
        configure?.Invoke(part);
        contentItem.Apply(part);

        return contentItem;
    }

    public static B2BAccount Create(
        string id = "account-1",
        string name = "Fabrikam Ltd",
        Action<B2BAccountPart> configure = null,
        bool published = true,
        AccountMember[] members = null,
        AccountLocation[] locations = null)
        => new(CreateItem(id, name, configure, published, members: members, locations: locations));
}
