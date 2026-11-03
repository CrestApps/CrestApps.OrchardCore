using CrestApps.Core;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Controllers;

/// <summary>
/// The QA dashboard: how the AI's scored conversations went over a period, for one QA cohort or all of them.
/// </summary>
[Admin]
[Feature(AIConstants.Feature.Quality)]
public sealed class AIQADashboardController : Controller
{
    private static readonly int[] _periods = [1, 7, 30, 90];

    private readonly AIQADashboardService _dashboardService;
    private readonly IAIQACohortStore _cohortStore;
    private readonly IAuthorizationService _authorizationService;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIQADashboardController"/> class.
    /// </summary>
    /// <param name="dashboardService">Reads the figures.</param>
    /// <param name="cohortStore">The QA cohort store.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="clock">The clock.</param>
    public AIQADashboardController(
        AIQADashboardService dashboardService,
        IAIQACohortStore cohortStore,
        IAuthorizationService authorizationService,
        IClock clock)
    {
        _dashboardService = dashboardService;
        _cohortStore = cohortStore;
        _authorizationService = authorizationService;
        _clock = clock;
    }

    /// <summary>
    /// Shows the dashboard.
    /// </summary>
    /// <param name="cohortId">The cohort, or empty for every scored conversation.</param>
    /// <param name="days">The period in days.</param>
    /// <returns>The dashboard.</returns>
    [Admin("ai/qa", "AIQADashboard")]
    public async Task<IActionResult> Index(string cohortId, int days = 7)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIQuality))
        {
            return Forbid();
        }

        days = _periods.Contains(days) ? days : 7;

        var cohorts = (await _cohortStore.GetAllAsync()).OrderBy(cohort => cohort.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var cohort = string.IsNullOrEmpty(cohortId) ? null : cohorts.FirstOrDefault(candidate => candidate.ItemId == cohortId);
        var toUtc = _clock.UtcNow;

        return View(new AIQADashboardViewModel
        {
            CohortId = cohort?.ItemId,
            Days = days,
            Cohorts = cohorts.Select(candidate => new SelectListItem(candidate.Name, candidate.ItemId, candidate.ItemId == cohort?.ItemId)).ToList(),
            Periods = _periods.Select(period => new SelectListItem(period.ToString(System.Globalization.CultureInfo.InvariantCulture), period.ToString(System.Globalization.CultureInfo.InvariantCulture), period == days)).ToList(),
            Dashboard = await _dashboardService.BuildAsync(cohort?.GetLabel(), toUtc.AddDays(-days), toUtc),
        });
    }
}
