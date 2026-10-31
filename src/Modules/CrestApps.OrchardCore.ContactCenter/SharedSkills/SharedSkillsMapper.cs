using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.ContactCenter.SharedSkills;

/// <summary>
/// Copies the fields Contact Center knows between its skill record and the shared catalog's.
/// </summary>
internal static class SharedSkillsMapper
{
    public static ContactCenterSkill ToContactCenter(Skill skill)
    {
        if (skill is null)
        {
            return null;
        }

        var copy = new ContactCenterSkill();

        CopyTo(skill, copy);

        return copy;
    }

    public static void CopyTo(Skill source, ContactCenterSkill target)
    {
        target.ItemId = source.ItemId;
        target.Name = source.Name;
        target.Description = source.Description;
        target.Enabled = source.Enabled;
        target.CreatedUtc = source.CreatedUtc;
        target.ModifiedUtc = source.ModifiedUtc;
    }

    public static void CopyTo(ContactCenterSkill source, Skill target)
    {
        target.ItemId = source.ItemId;
        target.Name = source.Name;
        target.Description = source.Description;
        target.Enabled = source.Enabled;

        if (source.CreatedUtc != default)
        {
            target.CreatedUtc = source.CreatedUtc;
        }
    }

    // Contact Center names a skill by its name, trimmed and compared without case.
    public static string NormalizeName(string name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? null
            : name.Trim().ToUpperInvariant();
    }
}
