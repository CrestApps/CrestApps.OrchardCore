using CrestApps.Core;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Staff.Core.Handlers;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Staff.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Staff.Drivers;

/// <summary>
/// The staff editor's own cards: the person, their employment and place, and the capabilities other modules register.
/// Other modules add their cards with their own drivers on <see cref="StaffMember"/>: Teams adds the team memberships,
/// Telephony the phone extension, Scheduling the booking settings, and so on.
/// </summary>
internal sealed class StaffMemberDisplayDriver : DisplayDriver<StaffMember>
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IStaffDirectory _directory;
    private readonly ITimeZoneSelectListProvider _timeZoneSelectListProvider;
    private readonly IClock _clock;
    private readonly IStringLocalizerFactory _localizerFactory;

    internal readonly IStringLocalizer S;

    public StaffMemberDisplayDriver(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IStaffDirectory directory,
        ITimeZoneSelectListProvider timeZoneSelectListProvider,
        IClock clock,
        IStringLocalizerFactory localizerFactory,
        IStringLocalizer<StaffMemberDisplayDriver> stringLocalizer)
    {
        _localizerFactory = localizerFactory;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _directory = directory;
        _timeZoneSelectListProvider = timeZoneSelectListProvider;
        _clock = clock;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(StaffMember staff, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape row)
        {
            row.Classes.Add("item");
        }

        return Combine(
            View("StaffMember_Fields_SummaryAdmin", staff)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("StaffMember_Tags_SummaryAdmin", staff)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("StaffMember_Buttons_SummaryAdmin", staff).Location("Actions:5"),
            View("StaffMember_ActionsMenu_SummaryAdmin", staff).Location("ActionsMenu:10"));
    }

    public override async Task<IDisplayResult> EditAsync(StaffMember staff, BuildEditorContext context)
    {
        var values = new StaffEditViewModel
        {
            DisplayName = staff.DisplayName,
            UserId = staff.UserId,
            Email = staff.Email,
            Phone = staff.Phone,
            EmployeeNumber = staff.EmployeeNumber,
            ExternalId = staff.ExternalId,
            JobTitle = staff.JobTitle,
            EmploymentType = staff.EmploymentType.ToString(),
            Status = staff.Status.ToString(),
            HireDate = staff.HireDate,
            SeniorityDate = staff.SeniorityDate,
            TerminationDate = staff.TerminationDate,
            DateOfBirth = staff.DateOfBirth,
            ManagerId = staff.ManagerId,
            TimeZoneId = staff.TimeZoneId,
            WorkLocation = staff.WorkLocation,
            HomeBase = staff.HomeBase?.Address,
            ContractedHoursPerWeek = staff.ContractedHoursPerWeek,
            Capabilities = staff.Capabilities.ToList(),
        };

        return await EditorAsync(staff, values);
    }

    public override async Task<IDisplayResult> UpdateAsync(StaffMember staff, UpdateEditorContext context)
    {
        var model = new StaffEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // A staff member keeps the user account it was linked to: pay, history and badges never move to another
        // person. A record without one can be linked once.
        if (string.IsNullOrEmpty(staff.UserId))
        {
            staff.UserId = string.IsNullOrWhiteSpace(model.UserId) ? null : model.UserId.Trim();
        }

        // A person with an account takes their name and e-mail from it when saved.
        if (string.IsNullOrEmpty(staff.UserId))
        {
            staff.DisplayName = model.DisplayName;
            staff.Email = model.Email;
        }

        staff.Phone = model.Phone;
        staff.EmployeeNumber = model.EmployeeNumber;
        staff.ExternalId = model.ExternalId;
        staff.JobTitle = model.JobTitle;
        staff.EmploymentType = Enum.TryParse<EmploymentType>(model.EmploymentType, out var employmentType) ? employmentType : EmploymentType.FullTime;
        staff.Status = Enum.TryParse<StaffStatus>(model.Status, out var status) ? status : StaffStatus.Active;
        staff.HireDate = model.HireDate;
        staff.SeniorityDate = model.SeniorityDate;
        staff.TerminationDate = model.TerminationDate;
        staff.ManagerId = string.IsNullOrWhiteSpace(model.ManagerId) ? null : model.ManagerId;
        staff.TimeZoneId = model.TimeZoneId;
        staff.WorkLocation = model.WorkLocation;
        staff.ContractedHoursPerWeek = model.ContractedHoursPerWeek;

        var homeBaseMoved = !string.Equals(staff.HomeBase?.Address, model.HomeBase?.Trim(), StringComparison.Ordinal);

        // A new address drops the old coordinates, so they are found again for the new one.
        staff.HomeBase = string.IsNullOrWhiteSpace(model.HomeBase) ? null : new StaffLocation
        {
            Address = model.HomeBase.Trim(),
            Latitude = homeBaseMoved ? null : staff.HomeBase?.Latitude,
            Longitude = homeBaseMoved ? null : staff.HomeBase?.Longitude,
        };
        // Only registered capabilities are edited here; ones a module set and doesn't show stay.
        var registered = _directory.GetCapabilities().Select(capability => capability.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var chosen = (model.Capabilities ?? []).Where(registered.Contains);

        staff.Capabilities = staff.Capabilities.Where(capability => !registered.Contains(capability)).Concat(chosen).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (await CanSeeSensitiveAsync())
        {
            staff.DateOfBirth = model.DateOfBirth;
        }

        return await EditorAsync(staff, model);
    }

    private async Task<IDisplayResult> EditorAsync(StaffMember staff, StaffEditViewModel values)
    {
        var canSeeSensitive = await CanSeeSensitiveAsync();
        var timeZoneOptions = await _timeZoneSelectListProvider.GetTimeZoneOptionsAsync(values.TimeZoneId);

        var selectedCapabilities = (values.Capabilities ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var capabilityOptions = _directory.GetCapabilities().Select(capability => new StaffCapabilityOptionViewModel
        {
            Name = capability.Name,
            DisplayName = capability.DisplayName is null ? capability.Name : _localizerFactory.Localize(capability.DisplayName).Value,
            Description = capability.Description is null ? null : _localizerFactory.Localize(capability.Description).Value,
            Selected = selectedCapabilities.Contains(capability.Name),
        }).ToList();

        var fields = Initialize<StaffEditViewModel>("StaffMemberFields_Edit", model =>
        {
            model.StaffMemberId = staff.ItemId;
            model.DisplayName = string.IsNullOrEmpty(values.UserId) ? values.DisplayName : staff.DisplayName ?? values.DisplayName;
            model.UserId = values.UserId;
            model.Email = string.IsNullOrEmpty(values.UserId) ? values.Email : staff.Email ?? values.Email;
            model.NameFromUser = !string.IsNullOrEmpty(values.UserId);
            model.UserLocked = staff.TryGet<StaffSavedState>(out var saved) && !string.IsNullOrEmpty(saved.UserId);
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
            model.ManagerId = values.ManagerId;
            model.TimeZoneId = values.TimeZoneId;
            model.WorkLocation = values.WorkLocation;
            model.HomeBase = values.HomeBase;
            model.ContractedHoursPerWeek = values.ContractedHoursPerWeek;
            model.EmploymentTypeOptions = StaffSelectLists.EmploymentTypes(S, values.EmploymentType);
            model.StatusOptions = StaffSelectLists.Statuses(S, values.Status);
            model.TimeZoneOptions = timeZoneOptions;
        }).Location("Content:1");

        if (capabilityOptions.Count == 0)
        {
            return fields;
        }

        // Its own shape, so a theme or module can move, replace or hide the card.
        var capabilities = Initialize<StaffCapabilitiesEditViewModel>("StaffCapabilities_Edit", model =>
        {
            model.Capabilities = values.Capabilities;
            model.CapabilityOptions = capabilityOptions;
        }).Location("Content:20");

        return Combine(fields, capabilities);
    }

    private async Task<bool> CanSeeSensitiveAsync()
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        return principal is not null && await _authorizationService.AuthorizeAsync(principal, StaffPermissions.ViewSensitiveStaffData);
    }
}

/// <summary>
/// The choices of employment type and status, for the editors and filters.
/// </summary>
internal static class StaffSelectLists
{
    public static List<SelectListItem> EmploymentTypes(IStringLocalizer S, string selected) =>
    [
        new(S["Full time"], nameof(EmploymentType.FullTime), selected == nameof(EmploymentType.FullTime)),
        new(S["Part time"], nameof(EmploymentType.PartTime), selected == nameof(EmploymentType.PartTime)),
        new(S["Seasonal"], nameof(EmploymentType.Seasonal), selected == nameof(EmploymentType.Seasonal)),
        new(S["Temporary"], nameof(EmploymentType.Temporary), selected == nameof(EmploymentType.Temporary)),
        new(S["Contractor"], nameof(EmploymentType.Contractor), selected == nameof(EmploymentType.Contractor)),
        new(S["Subcontractor"], nameof(EmploymentType.Subcontractor), selected == nameof(EmploymentType.Subcontractor)),
    ];

    public static List<SelectListItem> Statuses(IStringLocalizer S, string selected) =>
    [
        new(S["Active"], nameof(StaffStatus.Active), selected == nameof(StaffStatus.Active)),
        new(S["On leave"], nameof(StaffStatus.OnLeave), selected == nameof(StaffStatus.OnLeave)),
        new(S["Suspended"], nameof(StaffStatus.Suspended), selected == nameof(StaffStatus.Suspended)),
        new(S["Terminated"], nameof(StaffStatus.Terminated), selected == nameof(StaffStatus.Terminated)),
    ];
}
