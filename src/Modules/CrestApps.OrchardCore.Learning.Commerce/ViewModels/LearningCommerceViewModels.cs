using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Learning.Commerce.Models;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Learning.Commerce.ViewModels;

public class LearningProductPartViewModel
{
    public List<LearningProductGrantRow> Grants { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> Courses { get; set; } = [];
}

public class LearningProductGrantRow
{
    public bool Include { get; set; } = true;

    public LearningProductGrantKind Kind { get; set; }

    public string TargetId { get; set; }

    public int? AccessDays { get; set; }

    public int? Seats { get; set; }
}

public class LearningSeatsViewModel
{
    public string AccountName { get; set; }

    public IReadOnlyList<LearningSeatsPool> Pools { get; set; } = [];
}

public class LearningSeatsPool
{
    public LicensePool Pool { get; set; }

    public int Remaining { get; set; }

    public IReadOnlyList<LearningSeatsRow> Seats { get; set; } = [];

    public IReadOnlyList<AccountMember> MembersWithoutSeat { get; set; } = [];
}

public class LearningSeatsRow
{
    public LicenseSeat Seat { get; set; }

    public string Name { get; set; }

    public Enrollment Enrollment { get; set; }
}
