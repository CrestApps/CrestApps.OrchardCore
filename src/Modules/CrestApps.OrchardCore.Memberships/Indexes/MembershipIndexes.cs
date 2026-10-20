using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Memberships.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Memberships.Indexes;

public sealed class MembershipIndex : CatalogItemIndex
{
    public string UserId { get; set; }

    public string ContentItemId { get; set; }

    public string UserName { get; set; }

    public MembershipStatus Status { get; set; }

    public DateTime? EndsUtc { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public sealed class MembershipIndexProvider : IndexProvider<Membership>
{
    public MembershipIndexProvider()
    {
        CollectionName = MembershipsConstants.CollectionName;
    }

    public override void Describe(DescribeContext<Membership> context)
        => context.For<MembershipIndex>()
            .Map(membership => new MembershipIndex
            {
                ItemId = membership.ItemId,
                UserId = membership.UserId,
                ContentItemId = membership.ContentItemId,
                UserName = membership.UserName?.Length > 255 ? membership.UserName[..255] : membership.UserName,
                Status = membership.Status,
                EndsUtc = membership.EndsUtc,
                CreatedUtc = membership.CreatedUtc,
            });
}
