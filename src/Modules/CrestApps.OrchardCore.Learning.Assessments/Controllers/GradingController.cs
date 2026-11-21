using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Assessments.Controllers;

/// <summary>
/// The Learning grading page: the work of learners that waits for a grader, from every source (quiz attempts in Surveys,
/// coursework), in the courses the person grades.
/// </summary>
[Admin]
public sealed class GradingController : Controller
{
    private readonly GradingScope _scope;
    private readonly IUserSearchService _userSearchService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GradingController"/> class.
    /// </summary>
    /// <param name="scope">The grading scope of the person.</param>
    /// <param name="userSearchService">The user search service.</param>
    public GradingController(
        GradingScope scope,
        IUserSearchService userSearchService)
    {
        _scope = scope;
        _userSearchService = userSearchService;
    }

    /// <summary>
    /// Lists the work waiting for a grader in the courses the person grades: quiz attempts and coursework.
    /// </summary>
    /// <param name="pagerParameters">The pager.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <param name="queue">The grading queue.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    [Admin("learning/grading", "LearningGrading")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory, [FromServices] GradingQueue queue)
    {
        var courses = await _scope.GetCourseIdsAsync(User);

        if (courses is { Count: 0 })
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var page = await queue.PageAsync(courses, pager.Page, pager.PageSize, HttpContext.RequestAborted);
        var people = (await _userSearchService.GetAsync(page.Items.Select(item => item.UserId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal)))
            .ToDictionary(user => user.UserId, user => string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName, StringComparer.Ordinal);

        return View(new GradingListViewModel
        {
            List = new AdminListContext<GradingQueueRow>
            {
                Source = page.Items
                    .Select(item => new GradingQueueRow
                    {
                        Item = item,
                        LearnerName = item.UserId is not null && people.TryGetValue(item.UserId, out var name) ? name : item.UserId,
                        Url = Url.Action(item.Action, item.Controller, new RouteValueDictionary(item.RouteValues) { ["area"] = item.Area }),
                    })
                    .ToList(),
                Pager = await shapeFactory.PagerAsync(pager, page.Count, new RouteValueDictionary()),
            },
        });
    }

    /// <summary>
    /// Shows an attempt with its answers, the key and the grader guidance.
    /// </summary>
    /// <param name="id">The attempt.</param>
    /// <returns>The page.</returns>
}
