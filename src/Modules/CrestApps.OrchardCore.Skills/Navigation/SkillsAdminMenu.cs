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
        // Skills are shared by Workforce, Contact Center and other modules, so they sit under Content rather than any one of them.
        builder.Add(S["Content"], content => content
            .Add(S["Skills"], S["Skills"].PrefixPosition(), skills => skills
                .Id("skills")
                .Action("Index", "Skills", SkillsConstants.Features.Skills)
                .Permission(SkillsPermissions.ManageSkills)
                .LocalNav())
            .Add(S["Certification Types"], S["Certification Types"].PrefixPosition(), certifications => certifications
                .Id("certifications")
                .Action("Index", "Certifications", SkillsConstants.Features.Skills)
                .Permission(SkillsPermissions.ManageSkills)
                .LocalNav())
            .Add(S["Certificates"], S["Certificates"].PrefixPosition(), records => records
                .Id("certificationRecords")
                .Action("Index", "CertificationRecords", SkillsConstants.Features.Skills)
                .Permission(SkillsPermissions.ViewSkills)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
