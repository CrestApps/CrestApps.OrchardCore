using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The administration list of payment issues.
/// </summary>
public class PaymentIssuesAdminIndexViewModel
{
    /// <summary>
    /// Gets or sets the status to list, where an empty value lists every issue.
    /// </summary>
    public PaymentIssueStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the issues on this page, with the toolbar filter and the pager.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }
}
