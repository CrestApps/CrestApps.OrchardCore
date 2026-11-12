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
}

public class StaffProfilePageViewModel
{
    public string Id { get; set; }

    public string DisplayName { get; set; }

    public object Profile { get; set; }
}

public class StaffProfileHeaderViewModel
{
    public StaffMember StaffMember { get; set; }

    public string AvatarUrl { get; set; }

    public string UserName { get; set; }

    public string Initials { get; set; }

    public string TeamName { get; set; }

    public bool CanEdit { get; set; }
}

public class StaffReportingLineViewModel
{
    public string StaffMemberId { get; set; }

    public IList<StaffMember> ManagerChain { get; set; } = [];

    public IList<StaffMember> Reports { get; set; } = [];

    public bool CanAddReports { get; set; }
}

public class StaffHistoryViewModel
{
    public IList<StaffHistoryRowViewModel> History { get; set; } = [];
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

    public IList<string> Capabilities { get; set; } = [];

    [BindNever]
    public string StaffMemberId { get; set; }

    [BindNever]
    public bool NameFromUser { get; set; }

    [BindNever]
    public bool UserLocked { get; set; }

    [BindNever]
    public bool CanSeeSensitive { get; set; }

    [BindNever]
    public IList<SelectListItem> EmploymentTypeOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> StatusOptions { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> TimeZoneOptions { get; set; } = [];
}

public class StaffCapabilitiesEditViewModel
{
    public IList<string> Capabilities { get; set; } = [];

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

public class UserStaffViewModel
{
    public bool MakeStaff { get; set; }

    [BindNever]
    public string StaffMemberId { get; set; }

    [BindNever]
    public string JobTitle { get; set; }

    [BindNever]
    public string Status { get; set; }
}

public class StaffHomeViewModel
{
    public StaffMember StaffMember { get; set; }

    public IList<StaffMember> ManagerChain { get; set; } = [];

    public IList<StaffMember> Reports { get; set; } = [];

    public IList<string> Teams { get; set; } = [];

    public bool UsesTeams { get; set; }

    public List<CrestApps.OrchardCore.Staff.Services.StaffHomeLink> Links { get; set; } = [];
}

public class StaffOrgChartViewModel
{
    public IList<StaffOrgChartNode> Nodes { get; set; } = [];

    public int Count { get; set; }
}

/// <summary>
/// One person on the organization chart, linked to their manager by <see cref="ParentId"/>.
/// </summary>
public class StaffOrgChartNode
{
    public string Id { get; set; }

    public string ParentId { get; set; }

    public string Name { get; set; }

    public string JobTitle { get; set; }

    public string TeamName { get; set; }

    public string AvatarUrl { get; set; }

    public string Initials { get; set; }

    public string Url { get; set; }

    /// <summary>
    /// Gets the initials shown when a person has no picture: the first letters of the first and last words.
    /// </summary>
    /// <param name="name">The person's name.</param>
    /// <returns>Up to two upper-case letters.</returns>
    public static string InitialsOf(string name)
    {
        var words = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return words.Length switch
        {
            0 => "?",
            1 => words[0][..1].ToUpperInvariant(),
            _ => string.Concat(words[0][..1], words[^1][..1]).ToUpperInvariant(),
        };
    }
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

public class StaffSkillsProfileViewModel
{
    public string StaffMemberId { get; set; }

    public IList<StaffSkillRowViewModel> Skills { get; set; } = [];

    public IList<StaffCertificateRowViewModel> Certificates { get; set; } = [];

    public bool CanAdd { get; set; }

    public IList<SelectListItem> CertificationOptions { get; set; } = [];
}

public class StaffSkillRowViewModel
{
    public string Name { get; set; }

    public int Proficiency { get; set; }
}

public class StaffCertificateRowViewModel
{
    public string Id { get; set; }

    public string CertificationName { get; set; }

    public string Number { get; set; }

    public DateOnly? ExpiresOn { get; set; }

    public string Status { get; set; }
}

public class AddStaffCertificateViewModel
{
    public string StaffMemberId { get; set; }

    public string CertificationId { get; set; }

    public string Number { get; set; }

    public DateOnly? IssuedOn { get; set; }

    public DateOnly? ExpiresOn { get; set; }

    public bool Checked { get; set; }
}

public class StaffPayProfileViewModel
{
    public decimal? CurrentRate { get; set; }

    public decimal? AnnualSalary { get; set; }

    public decimal? HourlyCost { get; set; }

    public IList<CrestApps.OrchardCore.Staff.Drivers.StaffPayRateRowViewModel> Rates { get; set; } = [];
}
