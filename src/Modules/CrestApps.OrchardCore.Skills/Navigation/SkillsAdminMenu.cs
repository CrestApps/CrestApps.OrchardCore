using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Skills.Navigation;

/// <summary>
/// Adds the Skills, Certifications and Certificates items to the Workforce menu.
/// </summary>
public sealed class SkillsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkillsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SkillsAdminMenu(IStringLocalizer<SkillsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        // The first menu provider to add Workforce gives it its icon, and Skills comes before Workforce.
        builder.Add(S["Workforce"], S["Workforce"].PrefixPosition("6"), workforce => workforce
            .AddClass("workforce")
            .AddClass("icon-class-fa-solid")
            .AddClass("icon-class-fa-users")
            .Id("workforce")
            .Add(S["Skills"], S["Skills"].PrefixPosition("80"), skills => skills
                .Id("skills")
                .Action("Index", "Skills", SkillsConstants.Features.Skills)
                .Permission(SkillsPermissions.ManageSkills)
                .LocalNav())
            .Add(S["Certification Types"], S["Certification Types"].PrefixPosition("81"), certifications => certifications
                .Id("certifications")
                .Action("Index", "Certifications", SkillsConstants.Features.Skills)
                .Permission(SkillsPermissions.ManageSkills)
                .LocalNav())
            .Add(S["Certificates"], S["Certificates"].PrefixPosition("82"), records => records
                .Id("certificationRecords")
                .Action("Index", "CertificationRecords", SkillsConstants.Features.Skills)
                .Permission(SkillsPermissions.ViewSkills)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
