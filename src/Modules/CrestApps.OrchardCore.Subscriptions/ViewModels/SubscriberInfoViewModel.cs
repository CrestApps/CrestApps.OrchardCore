using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class SubscriberInfoViewModel
{
    public string DisplayName { get; set; }

    public string UserName { get; set; }

    public string Email { get; set; }

    public string UserId { get; set; }
}

public class SubscriberInvoicesViewModel
{
    public IList<SubscriberInvoiceViewModel> Invoices { get; set; }
}

public class SubscriberInvoiceViewModel
{
    public DateTime Date { get; set; }

    public string ServicePlanTitle { get; set; }

    public double Amount { get; set; }

    public PaymentStatus Status { get; set; }
}
