using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="ContactCenterSkill"/> documents to the <see cref="ContactCenterSkillIndex"/>.
/// </summary>
public sealed class ContactCenterSkillIndexProvider : IndexProvider<ContactCenterSkill>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSkillIndexProvider"/> class.
    /// </summary>
    public ContactCenterSkillIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ContactCenterSkill> context)
    {
        context
            .For<ContactCenterSkillIndex>()
            .Map(skill => new ContactCenterSkillIndex
            {
                ItemId = skill.ItemId,
                Name = skill.Name,
                Enabled = skill.Enabled,
            });
    }
}
