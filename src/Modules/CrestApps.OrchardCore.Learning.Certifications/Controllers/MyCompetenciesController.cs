using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Certifications.ViewModels;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Certifications.Controllers;

/// <summary>
/// The signed-in learner's competencies: the skills they hold today, by framework (the skill category), with the
/// level and the days they count. Skills given by hand, by a workflow or by completing learning all show.
/// </summary>
[Authorize]
[Feature(CertificationsConstants.Features.Competencies)]
public sealed class MyCompetenciesController : Controller
{
    private readonly ISkillAssignmentService _assignments;
    private readonly ISkillManager _skillManager;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public MyCompetenciesController(
        ISkillAssignmentService assignments,
        ISkillManager skillManager,
        ILocalClock localClock,
        IStringLocalizer<MyCompetenciesController> stringLocalizer)
    {
        _assignments = assignments;
        _skillManager = skillManager;
        _localClock = localClock;
        S = stringLocalizer;
    }

    [HttpGet("learning/competencies")]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);
        var skills = (await _skillManager.GetAllAsync()).ToDictionary(skill => skill.ItemId, StringComparer.Ordinal);
        var held = await _assignments.GetEffectiveAsync(SkillSubject.User(userId), today, HttpContext.RequestAborted);
        var noFramework = S["Other competencies"].Value;

        var frameworks = held
            .Where(assignment => skills.ContainsKey(assignment.SkillId))
            .Select(assignment => (Skill: skills[assignment.SkillId], Assignment: assignment))
            .GroupBy(item => string.IsNullOrEmpty(item.Skill.Category) ? noFramework : item.Skill.Category, StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(group => group.Key == noFramework ? 1 : 0)
            .ThenBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new MyCompetencyFramework
            {
                Name = group.Key,
                Competencies = group
                    .OrderBy(item => item.Skill.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Select(item => new MyCompetency
                    {
                        Name = item.Skill.Name,
                        Description = item.Skill.Description,
                        Proficiency = item.Assignment.Proficiency,
                        Since = item.Assignment.EffectiveFrom,
                        Until = item.Assignment.EffectiveTo,
                    })
                    .ToArray(),
            })
            .ToArray();

        return View(new MyCompetenciesViewModel { Frameworks = frameworks });
    }
}
