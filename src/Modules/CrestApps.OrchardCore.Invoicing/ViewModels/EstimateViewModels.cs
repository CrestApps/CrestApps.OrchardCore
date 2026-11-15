using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.Invoicing.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// The editor of a draft estimate: the invoice editor's header and lines, with the offer's own fields.
/// </summary>
public class EstimateEditViewModel : InvoiceEditViewModel
{
    public DateOnly? ExpiryDate { get; set; }

    public decimal? DepositPercent { get; set; }

    public decimal? DepositAmount { get; set; }

    public EstimateConversion OnAcceptance { get; set; }

    public bool CanSend { get; set; }
}

public class EstimatesAdminIndexOptions
{
    public string Search { get; set; }

    public EstimateStatus? Status { get; set; }

    public List<SelectListItem> Statuses { get; set; }
}

public class EstimatesAdminIndexViewModel
{
    public EstimatesAdminIndexOptions Options { get; set; } = new();

    public AdminListContext List { get; set; }

    public bool CanCreate { get; set; }
}

/// <summary>
/// A row of the estimates list.
/// </summary>
public class EstimateRowViewModel
{
    public Estimate Estimate { get; set; }

    public string BillToName { get; set; }

    public bool CanEdit { get; set; }
}

/// <summary>
/// An estimate's admin page.
/// </summary>
public class EstimateDetailViewModel
{
    public Estimate Estimate { get; set; }

    public BusinessDocument Document { get; set; }

    public string ShareUrl { get; set; }

    public bool CanManage { get; set; }

    public bool CanEmail { get; set; }

    public bool CanConvertDeposit { get; set; }

    public IReadOnlyDictionary<string, decimal> InvoicedPercents { get; set; } = new Dictionary<string, decimal>();

    public IReadOnlyList<Invoice> Invoices { get; set; } = [];
}

/// <summary>
/// An estimate's public page.
/// </summary>
public class EstimatePublicViewModel
{
    public Estimate Estimate { get; set; }

    public BusinessDocument Document { get; set; }

    public string Token { get; set; }

    public string PdfUrl { get; set; }

    public bool CanAnswer { get; set; }

    public bool IsExpired { get; set; }
}
