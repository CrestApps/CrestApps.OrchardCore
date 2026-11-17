using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Banking.Disbursements.ViewModels;

/// <summary>
/// The editor of a disbursement written by hand.
/// </summary>
public class DisbursementViewModel
{
    /// <summary>
    /// Gets or sets how the money is sent.
    /// </summary>
    public string Method { get; set; }

    /// <summary>
    /// Gets or sets the business's bank account the money leaves from.
    /// </summary>
    public string SourceBankAccountId { get; set; }

    /// <summary>
    /// Gets or sets the payee's bank account, for a bank file.
    /// </summary>
    public string DestinationBankAccountId { get; set; }

    /// <summary>
    /// Gets or sets who is paid.
    /// </summary>
    public string PayeeName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the payee is a business.
    /// </summary>
    public bool IsBusinessPayee { get; set; }

    /// <summary>
    /// Gets or sets the amount, as invariant-culture text.
    /// </summary>
    public string Amount { get; set; }

    /// <summary>
    /// Gets or sets a memo.
    /// </summary>
    public string Memo { get; set; }

    /// <summary>
    /// Gets or sets the methods.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> MethodOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the business's bank accounts.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> SourceAccountOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the payees' bank accounts.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> DestinationAccountOptions { get; set; } = [];
}

/// <summary>
/// The list of disbursements.
/// </summary>
public class DisbursementsIndexViewModel
{
    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the status filter.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the method filter.
    /// </summary>
    public string Method { get; set; }

    /// <summary>
    /// Gets or sets the batch filter.
    /// </summary>
    public string BatchId { get; set; }

    /// <summary>
    /// Gets or sets the date the money of a new batch should arrive, as yyyy-MM-dd; empty keeps the payments' own date.
    /// </summary>
    public string EffectiveDate { get; set; }

    /// <summary>
    /// Gets or sets the bulk action.
    /// </summary>
    public CatalogEntryOptions<string> Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the list.
    /// </summary>
    [BindNever]
    public AdminListContext<Disbursement> List { get; set; }

    /// <summary>
    /// Gets or sets the status options.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> StatusOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the method options.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> MethodOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the user may act on disbursements.
    /// </summary>
    [BindNever]
    public bool CanManage { get; set; }
}

/// <summary>
/// The filters in the toolbar of the list of disbursements.
/// </summary>
public class DisbursementsFiltersViewModel
{
    /// <summary>
    /// Gets or sets the status options.
    /// </summary>
    public IList<SelectListItem> StatusOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the method options.
    /// </summary>
    public IList<SelectListItem> MethodOptions { get; set; } = [];
}

/// <summary>
/// The list of batches.
/// </summary>
public class DisbursementBatchesIndexViewModel
{
    /// <summary>
    /// Gets or sets the status filter.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the list.
    /// </summary>
    [BindNever]
    public AdminListContext<DisbursementBatch> List { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Bank File (ACH) feature is on.
    /// </summary>
    [BindNever]
    public bool CanImportReturns { get; set; }
}

/// <summary>
/// The upload of a bank's return and change file.
/// </summary>
public class DisbursementReturnsViewModel
{
    /// <summary>
    /// Gets or sets what was applied, after an upload.
    /// </summary>
    [BindNever]
    public DisbursementImportResult Result { get; set; }
}

/// <summary>
/// The bank file settings card on one of the business's bank accounts.
/// </summary>
public class NachaOriginSettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the card was on the posted form. A form drawn while the account was not
    /// the business's (its owner changed in the same save) has no card, and its empty fields must not clear the settings.
    /// </summary>
    public bool Shown { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether bank files are sent from the account.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the routing number of the bank that receives the file.
    /// </summary>
    public string ImmediateDestination { get; set; }

    /// <summary>
    /// Gets or sets the name of the bank that receives the file.
    /// </summary>
    public string ImmediateDestinationName { get; set; }

    /// <summary>
    /// Gets or sets the origin.
    /// </summary>
    public string ImmediateOrigin { get; set; }

    /// <summary>
    /// Gets or sets the origin's name.
    /// </summary>
    public string ImmediateOriginName { get; set; }

    /// <summary>
    /// Gets or sets the company name.
    /// </summary>
    public string CompanyName { get; set; }

    /// <summary>
    /// Gets or sets the company identification.
    /// </summary>
    public string CompanyIdentification { get; set; }

    /// <summary>
    /// Gets or sets the entry description.
    /// </summary>
    public string CompanyEntryDescription { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether each batch ends with an offset debit.
    /// </summary>
    public bool IncludeOffset { get; set; }
}
