using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Workforce.Controllers;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users;
using OrchardCore.Users.Handlers;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Adds a Workforce section to the user editor, so a person is managed in one place: it makes the user a worker, or
/// edits their worker's employment and main team. The worker's name and email come from the user.
/// </summary>
internal sealed class UserWorkforceDisplayDriver : DisplayDriver<User>
{
    private readonly WorkerAccounts _accounts;
    private readonly IWorkerManager _workers;
    private readonly ITeamManager _teams;
    private readonly IWorkforceScopeService _scopeService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public UserWorkforceDisplayDriver(
        WorkerAccounts accounts,
        IWorkerManager workers,
        ITeamManager teams,
        IWorkforceScopeService scopeService,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<UserWorkforceDisplayDriver> stringLocalizer)
    {
        _accounts = accounts;
        _workers = workers;
        _teams = teams;
        _scopeService = scopeService;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(User user, BuildEditorContext context)
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        if (string.IsNullOrEmpty(user.UserId) || principal is null || !await _authorizationService.AuthorizeAsync(principal, WorkforcePermissions.ManageWorkers))
        {
            return null;
        }

        var worker = await _workers.FindByUserIdAsync(user.UserId);

        if (worker is not null && !await _scopeService.ManagesWorkerAsync(principal, worker))
        {
            return null;
        }

        var scope = await _scopeService.GetManagedScopeAsync(principal);
        var teamOptions = await PlanningOptions.TeamsAsync(_teams, scope, worker?.PrimaryTeamId, CancellationToken.None);

        return Initialize<UserWorkforceViewModel>("UserWorkforce_Edit", model =>
        {
            model.WorkerId = worker?.ItemId;
            model.EmployeeNumber = worker?.EmployeeNumber;
            model.JobTitle = worker?.JobTitle;
            model.EmploymentType = (worker?.EmploymentType ?? EmploymentType.FullTime).ToString();
            model.Status = (worker?.Status ?? WorkerStatus.Active).ToString();
            model.HireDate = worker?.HireDate;
            model.PrimaryTeamId = worker?.PrimaryTeamId;
            model.ContractedHoursPerWeek = worker?.ContractedHoursPerWeek;
            model.TeamOptions = teamOptions;
            model.EmploymentTypeOptions = EmploymentTypes(model.EmploymentType);
            model.StatusOptions = Statuses(model.Status);
        }).Location("Content:13");
    }

    public override async Task<IDisplayResult> UpdateAsync(User user, UpdateEditorContext context)
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        if (string.IsNullOrEmpty(user.UserId) || principal is null || !await _authorizationService.AuthorizeAsync(principal, WorkforcePermissions.ManageWorkers))
        {
            return null;
        }

        var model = new UserWorkforceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var worker = await _workers.FindByUserIdAsync(user.UserId);

        if (worker is null && !model.MakeWorker)
        {
            return await EditAsync(user, context);
        }

        if (worker is not null && !await _scopeService.ManagesWorkerAsync(principal, worker))
        {
            return await EditAsync(user, context);
        }

        var scope = await _scopeService.GetManagedScopeAsync(principal);

        if (!string.IsNullOrEmpty(model.PrimaryTeamId) && !scope.Covers(model.PrimaryTeamId))
        {
            context.Updater.ModelState.AddModelError(Prefix, S["You can only add workers to the teams you manage."]);

            return await EditAsync(user, context);
        }

        var result = await _accounts.SaveAsync(user.UserId, await DisplayNameAsync(user), user.Email, new UserWorkerInput
        {
            EmployeeNumber = model.EmployeeNumber,
            JobTitle = model.JobTitle,
            EmploymentType = Enum.TryParse<EmploymentType>(model.EmploymentType, out var employment) ? employment : EmploymentType.FullTime,
            Status = Enum.TryParse<WorkerStatus>(model.Status, out var status) ? status : WorkerStatus.Active,
            HireDate = model.HireDate,
            PrimaryTeamId = model.PrimaryTeamId,
            ContractedHoursPerWeek = model.ContractedHoursPerWeek,
        });

        foreach (var error in result.Errors)
        {
            context.Updater.ModelState.AddModelError(Prefix, error);
        }

        return await EditAsync(user, context);
    }

    private async Task<string> DisplayNameAsync(User user)
    {
        var provider = _httpContextAccessor.HttpContext?.RequestServices.GetService<IDisplayNameProvider>();

        return provider is null ? user.UserName : await provider.GetAsync(user) ?? user.UserName;
    }

    private IList<SelectListItem> EmploymentTypes(string selected) =>
    [
        new(S["Full time"], nameof(EmploymentType.FullTime), selected == nameof(EmploymentType.FullTime)),
        new(S["Part time"], nameof(EmploymentType.PartTime), selected == nameof(EmploymentType.PartTime)),
        new(S["Seasonal"], nameof(EmploymentType.Seasonal), selected == nameof(EmploymentType.Seasonal)),
        new(S["Temporary"], nameof(EmploymentType.Temporary), selected == nameof(EmploymentType.Temporary)),
        new(S["Contractor"], nameof(EmploymentType.Contractor), selected == nameof(EmploymentType.Contractor)),
        new(S["Subcontractor"], nameof(EmploymentType.Subcontractor), selected == nameof(EmploymentType.Subcontractor)),
    ];

    private IList<SelectListItem> Statuses(string selected) =>
    [
        new(S["Active"], nameof(WorkerStatus.Active), selected == nameof(WorkerStatus.Active)),
        new(S["On leave"], nameof(WorkerStatus.OnLeave), selected == nameof(WorkerStatus.OnLeave)),
        new(S["Suspended"], nameof(WorkerStatus.Suspended), selected == nameof(WorkerStatus.Suspended)),
        new(S["Terminated"], nameof(WorkerStatus.Terminated), selected == nameof(WorkerStatus.Terminated)),
    ];
}

/// <summary>
/// Gives a user's worker the user's name and email whenever the user is saved.
/// </summary>
internal sealed class WorkerUserEventHandler : UserEventHandlerBase
{
    private readonly IServiceProvider _serviceProvider;

    public WorkerUserEventHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async Task UpdatedAsync(UserUpdateContext context)
    {
        if (context.User is not User user || string.IsNullOrEmpty(user.UserId))
        {
            return;
        }

        var provider = _serviceProvider.GetService<IDisplayNameProvider>();
        var name = provider is null ? user.UserName : await provider.GetAsync(user) ?? user.UserName;

        await _serviceProvider.GetRequiredService<WorkerAccounts>().RefreshAsync(user.UserId, name, user.Email);
    }
}
