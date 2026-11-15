using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Ledgers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Learning.Certifications.ViewModels;

public class EducationCreditsPartViewModel
{
    public List<EducationCreditAwardRow> Awards { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> Types { get; set; } = [];
}

public class EducationCreditAwardRow
{
    public string CreditTypeId { get; set; }

    public decimal? Amount { get; set; }

    public int? ValidityMonths { get; set; }
}

public class EducationCreditsDisplayViewModel
{
    public IReadOnlyList<(string Name, string Unit, decimal Amount)> Awards { get; set; } = [];
}

public class EducationCreditTypeListViewModel
{
    public AdminListContext<EducationCreditType> List { get; set; }
}

public class EducationCreditTypeEditViewModel
{
    public string Id { get; set; }

    public string Name { get; set; }

    public string Unit { get; set; }

    public string Description { get; set; }

    public bool AllowExternalLearning { get; set; } = true;
}

public class ExternalLearningListViewModel
{
    public ExternalLearningState? State { get; set; }

    public AdminListContext<ExternalLearningSubmission> List { get; set; }
}

public class ExternalLearningRowViewModel
{
    public ExternalLearningSubmission Submission { get; set; }

    public string UserName { get; set; }

    public string TypeName { get; set; }

    public string Submitted { get; set; }
}

public class ExternalLearningReviewViewModel
{
    public string Id { get; set; }

    public string Note { get; set; }

    [BindNever]
    public ExternalLearningSubmission Submission { get; set; }

    [BindNever]
    public string UserName { get; set; }

    [BindNever]
    public string TypeName { get; set; }
}

public class MyCreditsViewModel
{
    public IReadOnlyList<MyCreditBalance> Balances { get; set; } = [];

    public IReadOnlyList<MyCreditEntry> Entries { get; set; } = [];

    public IReadOnlyList<MyExternalLearning> Submissions { get; set; } = [];

    public IList<SelectListItem> ExternalTypes { get; set; } = [];

    public MyExternalLearningForm Form { get; set; } = new();
}

public class MyCreditBalance
{
    public string Name { get; set; }

    public string Unit { get; set; }

    public decimal Amount { get; set; }
}

public class MyCreditEntry
{
    public QuantityLedgerEntry Entry { get; set; }

    public string TypeName { get; set; }

    public string Earned { get; set; }

    public string Expires { get; set; }
}

public class MyExternalLearning
{
    public ExternalLearningSubmission Submission { get; set; }

    public string TypeName { get; set; }

    public string Completed { get; set; }
}

public class MyExternalLearningForm
{
    public string Title { get; set; }

    public string Provider { get; set; }

    public DateOnly? CompletedOn { get; set; }

    public string CreditTypeId { get; set; }

    public decimal? Amount { get; set; }
}
