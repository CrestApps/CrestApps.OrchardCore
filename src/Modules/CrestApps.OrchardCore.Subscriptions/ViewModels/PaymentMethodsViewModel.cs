using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class PaymentMethodsViewModel
{
    public string PaymentMethod { get; set; }

    public List<SelectListItem> PaymentMethods { get; set; }
}
