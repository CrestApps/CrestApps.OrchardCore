using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// The worker's own section of the worker editor: who the worker is, their employment, teams, hours and availability.
/// Other modules add sections, such as Skills, with display drivers of their own for <see cref="WorkerProfile"/>.
/// </summary>
internal sealed class WorkerProfileDisplayDriver : DisplayDriver<WorkerProfile>
{
    private const int EmptyMembershipRows = 3;

    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWorkforceScopeService _scopeService;
    private readonly ITeamManager _teamManager;
    private readonly ITimeZoneSelectListProvider _timeZoneSelectListProvider;

    internal readonly IStringLocalizer S;

    public WorkerProfileDisplayDriver(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IWorkforceScopeService scopeService,
        ITeamManager teamManager,
        ITimeZoneSelectListProvider timeZoneSelectListProvider,
        IStringLocalizer<WorkerProfileDisplayDriver> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _scopeService = scopeService;
        _teamManager = teamManager;
        _timeZoneSelectListProvider = timeZoneSelectListProvider;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(WorkerProfile worker, BuildEditorContext context)
    {
        var values = new WorkerEditViewModel
        {
            DisplayName = worker.DisplayName,
            UserId = worker.UserId,
            Email = worker.Email,
            Phone = worker.Phone,
            EmployeeNumber = worker.EmployeeNumber,
            ExternalId = worker.ExternalId,
            JobTitle = worker.JobTitle,
            EmploymentType = worker.EmploymentType.ToString(),
            Status = worker.Status.ToString(),
            HireDate = worker.HireDate,
            SeniorityDate = worker.SeniorityDate,
            TerminationDate = worker.TerminationDate,
            TimeZoneId = worker.TimeZoneId,
            HomeBase = worker.HomeBase?.Address,
            ContractedHoursPerWeek = worker.ContractedHoursPerWeek,
            MinHoursPerWeek = worker.MinHoursPerWeek,
            MaxHoursPerWeek = worker.MaxHoursPerWeek,
            MaxHoursPerDay = worker.MaxHoursPerDay,
            Availability = AvailabilityFormat.Write(worker.Availability),
            DateOfBirth = worker.DateOfBirth,
            Memberships = worker.TeamMemberships.Select(membership => new TeamMembershipRowViewModel
            {
                TeamId = membership.TeamId,
                Role = membership.Role.ToString(),
                EffectiveFrom = membership.EffectiveFrom,
                EffectiveTo = membership.EffectiveTo,
            }).ToList(),
        };

        return await EditorAsync(values);
    }

    public override async Task<IDisplayResult> UpdateAsync(WorkerProfile worker, UpdateEditorContext context)
    {
        var model = new WorkerEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var principal = _httpContextAccessor.HttpContext?.User;
        var scope = await _scopeService.GetManagedScopeAsync(principal);
        var availabilityErrors = new List<string>();
        var availability = AvailabilityFormat.Read(model.Availability, availabilityErrors);

        foreach (var error in availabilityErrors)
        {
            context.Updater.ModelState.AddModelError(Key(nameof(WorkerEditViewModel.Availability)), S["This line can't be read: {0}", error]);
        }

        var memberships = (model.Memberships ?? [])
            .Where(row => !string.IsNullOrEmpty(row.TeamId))
            .Select(row => new TeamMembership
            {
                TeamId = row.TeamId,
                Role = Enum.TryParse<TeamMemberRole>(row.Role, out var role) ? role : TeamMemberRole.Member,
                EffectiveFrom = row.EffectiveFrom,
                EffectiveTo = row.EffectiveTo,
            })
            .ToList();

        // A manager can only place workers in the teams they manage.
        if (!scope.All && memberships.Any(membership => !scope.Covers(membership.TeamId)))
        {
            context.Updater.ModelState.AddModelError(Key(nameof(WorkerEditViewModel.Memberships)), S["You can only add workers to the teams you manage."]);
        }

        worker.DisplayName = model.DisplayName;
        worker.UserId = model.UserId;
        worker.Email = model.Email;

        // A worker with an account takes its name and email from it, as the user editor does.
        if (!string.IsNullOrEmpty(model.UserId)
            && _httpContextAccessor.HttpContext?.RequestServices.GetService<UserManager<IUser>>() is { } userManager
            && await userManager.FindByIdAsync(model.UserId) is User account)
        {
            var names = _httpContextAccessor.HttpContext.RequestServices.GetService<IDisplayNameProvider>();

            worker.DisplayName = (names is null ? null : await names.GetAsync(account)) ?? account.UserName;
            worker.Email = account.Email;
        }

        worker.Phone = model.Phone;
        worker.EmployeeNumber = model.EmployeeNumber;
        worker.ExternalId = model.ExternalId;
        worker.JobTitle = model.JobTitle;
        worker.EmploymentType = Enum.TryParse<EmploymentType>(model.EmploymentType, out var employmentType) ? employmentType : EmploymentType.FullTime;
        worker.Status = Enum.TryParse<WorkerStatus>(model.Status, out var status) ? status : WorkerStatus.Active;
        worker.HireDate = model.HireDate;
        worker.SeniorityDate = model.SeniorityDate;
        worker.TerminationDate = model.TerminationDate;
        worker.TimeZoneId = model.TimeZoneId;

        var homeBaseMoved = !string.Equals(worker.HomeBase?.Address, model.HomeBase?.Trim(), StringComparison.Ordinal);

        // A new address drops the old coordinates, so they are found again for the new one.
        worker.HomeBase = string.IsNullOrWhiteSpace(model.HomeBase) ? null : new WorkforceLocation
        {
            Address = model.HomeBase.Trim(),
            Latitude = homeBaseMoved ? null : worker.HomeBase?.Latitude,
            Longitude = homeBaseMoved ? null : worker.HomeBase?.Longitude,
        };
        worker.ContractedHoursPerWeek = model.ContractedHoursPerWeek;
        worker.MinHoursPerWeek = model.MinHoursPerWeek;
        worker.MaxHoursPerWeek = model.MaxHoursPerWeek;
        worker.MaxHoursPerDay = model.MaxHoursPerDay;
        worker.Availability = availability;
        worker.TeamMemberships = memberships;
        worker.PrimaryTeamId = memberships.FirstOrDefault()?.TeamId;

        if (await CanSeeSensitiveAsync())
        {
            worker.DateOfBirth = model.DateOfBirth;
        }

        // The posted values are shown again as typed, so a line of availability that can't be read stays to be fixed.
        model.DisplayName = worker.DisplayName;
        model.Email = worker.Email;

        return await EditorAsync(model);
    }

    private async Task<IDisplayResult> EditorAsync(WorkerEditViewModel values)
    {
        var canSeeSensitive = await CanSeeSensitiveAsync();
        var scope = await _scopeService.GetManagedScopeAsync(_httpContextAccessor.HttpContext?.User);
        var teamOptions = (await _teamManager.ListTreeAsync())
            .Where(item => scope.Covers(item.Team.ItemId))
            .Select(item => new SelectListItem(new string(' ', item.Depth * 4) + item.Team.Name, item.Team.ItemId))
            .ToList();
        var timeZoneOptions = await _timeZoneSelectListProvider.GetTimeZoneOptionsAsync(values.TimeZoneId);
        var memberships = (values.Memberships ?? []).ToList();

        while (memberships.Count(row => string.IsNullOrEmpty(row.TeamId)) < EmptyMembershipRows)
        {
            memberships.Add(new TeamMembershipRowViewModel { Role = nameof(TeamMemberRole.Member) });
        }

        return Initialize<WorkerEditViewModel>("WorkerProfileFields_Edit", model =>
        {
            model.DisplayName = values.DisplayName;
            model.UserId = values.UserId;
            model.Email = values.Email;
            model.NameFromUser = !string.IsNullOrEmpty(values.UserId);
            model.Phone = values.Phone;
            model.EmployeeNumber = values.EmployeeNumber;
            model.ExternalId = values.ExternalId;
            model.JobTitle = values.JobTitle;
            model.EmploymentType = values.EmploymentType;
            model.Status = values.Status;
            model.HireDate = values.HireDate;
            model.SeniorityDate = values.SeniorityDate;
            model.TerminationDate = values.TerminationDate;
            model.CanSeeSensitive = canSeeSensitive;
            model.DateOfBirth = canSeeSensitive ? values.DateOfBirth : null;
            model.TimeZoneId = values.TimeZoneId;
            model.HomeBase = values.HomeBase;
            model.ContractedHoursPerWeek = values.ContractedHoursPerWeek;
            model.MinHoursPerWeek = values.MinHoursPerWeek;
            model.MaxHoursPerWeek = values.MaxHoursPerWeek;
            model.MaxHoursPerDay = values.MaxHoursPerDay;
            model.Availability = values.Availability;
            model.Memberships = memberships;
            model.TeamOptions = teamOptions;
            model.EmploymentTypeOptions = EmploymentTypeOptions(values.EmploymentType);
            model.StatusOptions = StatusOptions(values.Status);
            model.TimeZoneOptions = timeZoneOptions;
        }).Location("Content:1");
    }

    private async Task<bool> CanSeeSensitiveAsync()
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        return principal is not null && await _authorizationService.AuthorizeAsync(principal, WorkforcePermissions.ViewSensitiveWorkerData);
    }

    private string Key(string name)
        => string.IsNullOrEmpty(Prefix) ? name : $"{Prefix}.{name}";

    private List<SelectListItem> EmploymentTypeOptions(string selected)
    {
        return
        [
            new SelectListItem(S["Full time"], nameof(EmploymentType.FullTime), selected == nameof(EmploymentType.FullTime)),
            new SelectListItem(S["Part time"], nameof(EmploymentType.PartTime), selected == nameof(EmploymentType.PartTime)),
            new SelectListItem(S["Seasonal"], nameof(EmploymentType.Seasonal), selected == nameof(EmploymentType.Seasonal)),
            new SelectListItem(S["Temporary"], nameof(EmploymentType.Temporary), selected == nameof(EmploymentType.Temporary)),
            new SelectListItem(S["Contractor"], nameof(EmploymentType.Contractor), selected == nameof(EmploymentType.Contractor)),
            new SelectListItem(S["Subcontractor"], nameof(EmploymentType.Subcontractor), selected == nameof(EmploymentType.Subcontractor)),
        ];
    }

    private List<SelectListItem> StatusOptions(string selected)
    {
        return
        [
            new SelectListItem(S["Active"], nameof(WorkerStatus.Active), selected == nameof(WorkerStatus.Active)),
            new SelectListItem(S["On leave"], nameof(WorkerStatus.OnLeave), selected == nameof(WorkerStatus.OnLeave)),
            new SelectListItem(S["Suspended"], nameof(WorkerStatus.Suspended), selected == nameof(WorkerStatus.Suspended)),
            new SelectListItem(S["Terminated"], nameof(WorkerStatus.Terminated), selected == nameof(WorkerStatus.Terminated)),
        ];
    }
}
