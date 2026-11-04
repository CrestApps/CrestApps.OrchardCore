using CrestApps.OrchardCore.Skills;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// Contact Center keeps its skills in the shared Skills catalog, so its old skill pages lead to the Skills pages.
/// </summary>
public sealed class SkillsController : Controller
{
    /// <summary>
    /// Goes to the skills list.
    /// </summary>
    /// <returns>A redirect to the Skills module's list.</returns>
    [Admin("contact-center/skills", "ContactCenterSkillsIndex")]
    public IActionResult Index()
        => RedirectToAction("Index", "Skills", new { area = SkillsConstants.Features.Skills });

    /// <summary>
    /// Goes to the new skill editor.
    /// </summary>
    /// <returns>A redirect to the Skills module's editor.</returns>
    [Admin("contact-center/skills/create", "ContactCenterSkillsCreate")]
    public IActionResult Create()
        => RedirectToAction("Edit", "Skills", new { area = SkillsConstants.Features.Skills });

    /// <summary>
    /// Goes to a skill's editor.
    /// </summary>
    /// <param name="id">The skill, with the same id in the catalog.</param>
    /// <returns>A redirect to the Skills module's editor.</returns>
    [Admin("contact-center/skills/edit/{id}", "ContactCenterSkillsEdit")]
    public IActionResult Edit(string id)
        => RedirectToAction("Edit", "Skills", new { area = SkillsConstants.Features.Skills, id });
}
