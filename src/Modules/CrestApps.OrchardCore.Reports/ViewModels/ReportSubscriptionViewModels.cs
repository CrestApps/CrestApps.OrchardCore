using CrestApps.OrchardCore.Reports.Subscriptions.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Reports.ViewModels;

public class ReportSubscriptionsIndexViewModel
{
    public AdminListContext<ReportSubscription> List { get; set; }

    public string Search { get; set; }
}

public class ReportSubscriptionEditViewModel
{
    public string Name { get; set; }

    public string ReportName { get; set; }

    public ReportSubscriptionFrequency Frequency { get; set; } = ReportSubscriptionFrequency.Weekly;

    public DayOfWeek DayOfWeek { get; set; } = DayOfWeek.Monday;

    public int DayOfMonth { get; set; } = 1;

    public int Hour { get; set; } = 7;

    public string Recipients { get; set; }

    public string AttachmentFormat { get; set; }

    public bool IsEnabled { get; set; } = true;

    [BindNever]
    public ReportSubscription Subscription { get; set; }

    [BindNever]
    public IList<SelectListItem> Reports { get; set; } = [];

    [BindNever]
    public IList<SelectListItem> Formats { get; set; } = [];

    [BindNever]
    public bool CanEmail { get; set; }

    [BindNever]
    public bool IsNew
        => Subscription is null;
}
