using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Programs.Core;
using CrestApps.OrchardCore.Learning.Programs.Drivers;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Learning.Programs.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Routing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Programs.Controllers;

/// <summary>
/// The learner's required learning and learning paths.
/// </summary>
[Authorize]
public sealed class MyRequiredLearningController : Controller
{
    private readonly ILearningPathService _paths;
    private readonly IContentManager _contentManager;
    private readonly ILocalClock _localClock;

    public MyRequiredLearningController(ILearningPathService paths, IContentManager contentManager, ILocalClock localClock)
    {
        _paths = paths;
        _contentManager = contentManager;
        _localClock = localClock;
    }

    /// <summary>
    /// Lists what is required of the signed-in person and the paths they are on.
    /// </summary>
    [HttpGet]
    [Route("learning/required")]
    public async Task<IActionResult> Index([FromServices] IServiceProvider services)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var required = new List<MyRequiredLearningItem>();

        if (services.GetService<IRequiredLearningService>() is { } requiredLearning)
        {
            var targets = services.GetServices<IRequiredLearningTarget>().ToArray();

            foreach (var assignment in (await requiredLearning.ListForUsersAsync([userId], HttpContext.RequestAborted)).Where(assignment => assignment.State != RequiredLearningState.NotRequired))
            {
                var rule = await requiredLearning.FindByIdAsync(assignment.RequirementId, HttpContext.RequestAborted);

                if (rule is null)
                {
                    continue;
                }

                var target = targets.FirstOrDefault(candidate => candidate.Kind == rule.TargetKind);

                required.Add(new MyRequiredLearningItem
                {
                    Assignment = assignment,
                    Name = target is null ? rule.Name : await target.GetDisplayTextAsync(rule.TargetId, HttpContext.RequestAborted) ?? rule.Name,
                    TargetKind = rule.TargetKind,
                    TargetId = rule.TargetId,
                    Due = await ProgramsDates.FormatAsync(_localClock, assignment.DueUtc),
                });
            }
        }

        var paths = new List<MyLearningPathItem>();

        foreach (var progress in await _paths.ListForUserAsync(userId, HttpContext.RequestAborted))
        {
            var path = await _contentManager.GetAsync(progress.PathId, VersionOptions.Published);

            if (path is null || !path.TryGet<LearningPathPart>(out var part))
            {
                continue;
            }

            var route = await _contentManager.PopulateAspectAsync<ContentItemMetadata>(path);

            paths.Add(new MyLearningPathItem
            {
                Progress = progress,
                Title = path.DisplayText,
                Url = route.DisplayRouteValues is null ? null : Url.RouteUrl(route.DisplayRouteValues),
                StageCount = LearningPathRules.Stages(part).Count,
            });
        }

        return View(new MyRequiredLearningViewModel
        {
            Required = required
                .OrderBy(item => item.Assignment.IsOpen ? 0 : 1)
                .ThenBy(item => item.Assignment.DueUtc ?? DateTime.MaxValue)
                .ToArray(),
            Paths = paths.OrderBy(item => item.Progress.State).ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase).ToArray(),
        });
    }
}
