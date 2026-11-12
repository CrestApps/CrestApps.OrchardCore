using CrestApps.OrchardCore.Staff.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Staff.ViewModels;

public class StaffListViewModel
{
    public string Search { get; set; }

    public string TeamId { get; set; }

    public string Status { get; set; }

    public string Capability { get; set; }

    [BindNever]
    public AdminListContext List { get; set; }

    [BindNever]
    public IList<SelectListItem> TeamOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> StatusOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> CapabilityOptions { get; set; } = [];

    [BindNever]
    public bool CanCreate { get; set; }
}

public class StaffEditorPageViewModel
{
    public string Id { get; set; }

    public string DisplayName { get; set; }

    public object Editor { get; set; }

    public IList<StaffHistoryRowViewModel> History { get; set; } = [];

    public IList<StaffMember> Reports { get; set; } = [];

    public IList<StaffMember> ManagerChain { get; set; } = [];
}

public class StaffHistoryRowViewModel
{
    public string Field { get; set; }

    public string From { get; set; }

    public string To { get; set; }

    public string ChangedBy { get; set; }

    public DateTime ChangedLocal { get; set; }
}

public class StaffEditViewModel
{
    public string DisplayName { get; set; }

    public string UserId { get; set; }

    public string Email { get; set; }

    public string Phone { get; set; }

    public string EmployeeNumber { get; set; }

    public string ExternalId { get; set; }

    public string JobTitle { get; set; }

    public string EmploymentType { get; set; }

    public string Status { get; set; }

    public DateOnly? HireDate { get; set; }

    public DateOnly? SeniorityDate { get; set; }

    public DateOnly? TerminationDate { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string ManagerId { get; set; }

    public string TimeZoneId { get; set; }

    public string WorkLocation { get; set; }

    public string HomeBase { get; set; }

    public decimal? ContractedHoursPerWeek { get; set; }

    public IList<TeamMembershipRowViewModel> Memberships { get; set; } = [];

    public IList<string> Capabilities { get; set; } = [];

    [BindNever]
    public string StaffMemberId { get; set; }

    [BindNever]
    public bool NameFromUser { get; set; }

    [BindNever]
    public bool CanSeeSensitive { get; set; }

    [BindNever]
    public IList<SelectListItem> TeamOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> EmploymentTypeOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> StatusOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> TimeZoneOptions { get; set; } = [];

    [BindNever]
    public IList<StaffCapabilityOptionViewModel> CapabilityOptions { get; set; } = [];
}

public class StaffCapabilityOptionViewModel
{
    public string Name { get; set; }

    public string DisplayName { get; set; }

    public string Description { get; set; }

    public bool Selected { get; set; }
}

public class TeamMembershipRowViewModel
{
    public string TeamId { get; set; }

    public string Role { get; set; }

    public DateOnly? EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }
}

public class UserStaffViewModel
{
    public bool MakeStaff { get; set; }

    public string EmployeeNumber { get; set; }

    public string JobTitle { get; set; }

    public string EmploymentType { get; set; }

    public string Status { get; set; }

    public DateOnly? HireDate { get; set; }

    public string PrimaryTeamId { get; set; }

    public string ManagerId { get; set; }

    public decimal? ContractedHoursPerWeek { get; set; }

    [BindNever]
    public string StaffMemberId { get; set; }

    [BindNever]
    public IList<SelectListItem> TeamOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> EmploymentTypeOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> StatusOptions { get; set; } = [];
}

public class StaffHomeViewModel
{
    public StaffMember StaffMember { get; set; }

    public IList<StaffMember> ManagerChain { get; set; } = [];

    public IList<StaffMember> Reports { get; set; } = [];

    public IList<string> Teams { get; set; } = [];

    public List<CrestApps.OrchardCore.Staff.Services.StaffHomeLink> Links { get; set; } = [];
}

public class StaffOrgChartViewModel
{
    public IList<StaffOrgChartNode> Roots { get; set; } = [];

    public int Count { get; set; }
}

public class StaffOrgChartNode
{
    public StaffMember StaffMember { get; set; }

    public string TeamName { get; set; }

    public IList<StaffOrgChartNode> Reports { get; set; } = [];
}

public class StaffPickerViewModel
{
    public string Id { get; set; }

    public string InputName { get; set; }

    public string Capability { get; set; }

    public bool Multiple { get; set; }

    public string ValueType { get; set; }

    public string ButtonText { get; set; }

    public string InitialItemsJson { get; set; }

    public bool SmallButton { get; set; }

    public bool Required { get; set; }

    public string ExcludeId { get; set; }
}
