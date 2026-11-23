using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.MailingLists.Core.Services;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Recipes;
using CrestApps.OrchardCore.MailingLists.Services;

namespace CrestApps.OrchardCore.MailingLists.Deployments;

/// <summary>
/// Writes the members of every mailing list into a deployment plan's recipe, a page at a time per list.
/// </summary>
internal sealed class MailingListMembersDeploymentSource : CatalogDeploymentSourceBase<MailingListMembersDeploymentStep, MailingListMember>
{
    private const int PageSize = 500;

    private readonly IMailingListManager _lists;
    private readonly IMailingListMemberStore _members;

    public MailingListMembersDeploymentSource(
        IMailingListManager lists,
        IMailingListMemberStore members)
        : base(MailingListMembersRecipeStep.EntriesName)
    {
        _lists = lists;
        _members = members;
    }

    protected override async Task<IEnumerable<MailingListMember>> GetEntriesAsync()
    {
        var entries = new List<MailingListMember>();

        foreach (var list in await _lists.GetAllAsync())
        {
            for (var skip = 0; ; skip += PageSize)
            {
                var (_, page) = await _members.PageAsync(new MailingListMemberQuery { ListId = list.ItemId }, skip, PageSize);

                entries.AddRange(page);

                if (page.Count < PageSize)
                {
                    break;
                }
            }
        }

        return entries;
    }
}
