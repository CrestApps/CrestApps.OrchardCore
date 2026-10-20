using CrestApps.OrchardCore.Memberships.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Memberships.ViewModels;

public class MembershipPartViewModel
{
    public string[] RoleNames { get; set; } = [];

    public int DurationDays { get; set; }

    [BindNever]
    public IReadOnlyList<string> AvailableRoles { get; set; } = [];
}

public class MembershipsIndexViewModel
{
    public string Search { get; set; }

    public MembershipStatus? Status { get; set; }

    [BindNever]
    public AdminListContext<Membership> List { get; set; }
}

public class AccountMembershipsViewModel
{
    public IList<Membership> Memberships { get; set; } = [];
}
