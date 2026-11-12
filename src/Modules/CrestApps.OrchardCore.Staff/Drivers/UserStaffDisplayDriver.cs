using CrestApps.OrchardCore.Staff.Core.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Staff.ViewModels;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users.Handlers;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Staff.Drivers;

/// <summary>
/// The Staff section of the user editor: makes the person a staff member, or edits their staff record in place.
/// </summary>
internal sealed class UserStaffDisplayDriver : DisplayDriver<User>
{
    private readonly StaffAccounts _accounts;
    private readonly IStaffMemberManager _staffManager;
    private readonly IStaffScopeService _scopeService;
    private readonly StaffTeamReader _teams;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public UserStaffDisplayDriver(
        StaffAccounts accounts,
        IStaffMemberManager staffManager,
        IStaffScopeService scopeService,
        StaffTeamReader teams,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<UserStaffDisplayDriver> stringLocalizer)
    {
        _accounts = accounts;
        _staffManager = staffManager;
        _scopeService = scopeService;
        _teams = teams;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(User user, BuildEditorContext context)
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        if (string.IsNullOrEmpty(user.UserId) || principal is null)
        {
            return null;
        }

        var scope = await _scopeService.GetScopeAsync(principal);

        if (scope.IsEmpty)
        {
            return null;
        }

        var staff = await _staffManager.FindByUserIdAsync(user.UserId);

        if (staff is not null && !await _scopeService.CanManageAsync(principal, staff))
        {
            return null;
        }

        var teamOptions = (await _teams.ListTreeAsync())
            .Where(item => scope.CoversTeam(item.Team.ItemId))
            .Select(item => new SelectListItem(new string(' ', item.Depth * 4) + item.Team.Name, item.Team.ItemId, item.Team.ItemId == staff?.PrimaryTeamId))
            .ToList();

        return Initialize<UserStaffViewModel>("UserStaff_Edit", model =>
        {
            model.StaffMemberId = staff?.ItemId;
            model.EmployeeNumber = staff?.EmployeeNumber;
            model.JobTitle = staff?.JobTitle;
            model.ManagerId = staff?.ManagerId;
            model.EmploymentType = (staff?.EmploymentType ?? EmploymentType.FullTime).ToString();
            model.Status = (staff?.Status ?? StaffStatus.Active).ToString();
            model.HireDate = staff?.HireDate;
            model.PrimaryTeamId = staff?.PrimaryTeamId;
            model.ContractedHoursPerWeek = staff?.ContractedHoursPerWeek;
            model.TeamOptions = teamOptions;
            model.EmploymentTypeOptions = StaffSelectLists.EmploymentTypes(S, model.EmploymentType);
            model.StatusOptions = StaffSelectLists.Statuses(S, model.Status);
        }).Location("Content:13");
    }

    public override async Task<IDisplayResult> UpdateAsync(User user, UpdateEditorContext context)
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        if (string.IsNullOrEmpty(user.UserId) || principal is null)
        {
            return null;
        }

        var scope = await _scopeService.GetScopeAsync(principal);

        if (scope.IsEmpty)
        {
            return null;
        }

        var model = new UserStaffViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var staff = await _staffManager.FindByUserIdAsync(user.UserId);

        if (staff is null && !model.MakeStaff)
        {
            return await EditAsync(user, context);
        }

        if (staff is not null && !await _scopeService.CanManageAsync(principal, staff))
        {
            return await EditAsync(user, context);
        }

        if (!string.IsNullOrEmpty(model.PrimaryTeamId) && !scope.CoversTeam(model.PrimaryTeamId) && staff?.PrimaryTeamId != model.PrimaryTeamId)
        {
            context.Updater.ModelState.AddModelError(Prefix, S["You can only add people to the teams you manage."]);

            return await EditAsync(user, context);
        }

        var result = await _accounts.SaveAsync(user.UserId, user.UserName, new StaffAccountInput
        {
            EmployeeNumber = model.EmployeeNumber,
            JobTitle = model.JobTitle,
            ManagerId = model.ManagerId,
            EmploymentType = Enum.TryParse<EmploymentType>(model.EmploymentType, out var employment) ? employment : EmploymentType.FullTime,
            Status = Enum.TryParse<StaffStatus>(model.Status, out var status) ? status : StaffStatus.Active,
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
}

/// <summary>
/// Keeps a staff member's name and e-mail in step with their user when the user changes.
/// </summary>
internal sealed class StaffUserEventHandler : UserEventHandlerBase
{
    private readonly IServiceProvider _serviceProvider;

    public StaffUserEventHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override async Task UpdatedAsync(UserUpdateContext context)
    {
        if (context.User is not User user || string.IsNullOrEmpty(user.UserId))
        {
            return;
        }

        // The staff manager is resolved here, not injected: the user manager constructs every user event handler.
        var manager = _serviceProvider.GetRequiredService<IStaffMemberManager>();
        var staff = await manager.FindByUserIdAsync(user.UserId);

        if (staff is null)
        {
            return;
        }

        var provider = _serviceProvider.GetService<IDisplayNameProvider>();
        var name = provider is null ? user.UserName : await provider.GetAsync(user) ?? user.UserName;

        if (string.Equals(staff.DisplayName, name, StringComparison.Ordinal) && string.Equals(staff.Email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // The save takes the name and e-mail from the user again.
        await manager.UpdateAsync(staff);
    }
}
