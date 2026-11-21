using CrestApps.OrchardCore.Learning.Certifications.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Learning.Certifications.ViewModels;

public class CertificationPathViewModel
{
    public bool Earned { get; set; }

    public bool Enabled { get; set; } = true;

    public RequirementJoin Join { get; set; }

    public List<CertificationRequirementRow> InitialRequirements { get; set; } = [];

    public List<CertificationRequirementRow> RenewalRequirements { get; set; } = [];

    public CertificationReferenceDate ReferenceDate { get; set; }

    public int FixedMonth { get; set; } = 12;

    public int FixedDay { get; set; } = 31;

    public int MinimumIntervalMonths { get; set; }

    public int RenewalWindowDays { get; set; } = 60;

    public int GracePeriodDays { get; set; }

    public bool StartsOnFirstRequirement { get; set; } = true;

    public bool EnrollOnRenewal { get; set; } = true;

    [BindNever]
    public string PathId { get; set; }

    [BindNever]
    public List<SelectListItem> KindOptions { get; set; } = [];

    [BindNever]
    public List<SelectListItem> TargetOptions { get; set; } = [];
}

public class CertificationRequirementRow
{
    public string Id { get; set; }

    public string Kind { get; set; }

    public string TargetId { get; set; }

    public decimal? MinimumGrade { get; set; }
}

public class CertificationPathPeopleViewModel
{
    public CertificationPath Path { get; set; }

    public string CertificationName { get; set; }

    public CertificationPathState? State { get; set; }

    public string UserName { get; set; }

    [BindNever]
    public AdminListContext<CertificationPathProgress> List { get; set; }

    [BindNever]
    public List<SelectListItem> StateOptions { get; set; } = [];
}

public class CertificationPathProgressRowViewModel
{
    public CertificationPathProgress Progress { get; set; }

    public string UserName { get; set; }
}

public class MyCertificationsViewModel
{
    public List<MyCertificationEntry> Entries { get; set; } = [];
}

public class MyCertificationEntry
{
    public CertificationPathProgress Progress { get; set; }

    public string CertificationName { get; set; }

    public string Number { get; set; }

    public string VerificationCode { get; set; }

    public DateOnly? IssuedOn { get; set; }

    public List<MyCertificationCourse> RenewalCourses { get; set; } = [];
}

public class MyCertificationCourse
{
    public string CourseId { get; set; }

    public string Title { get; set; }
}
