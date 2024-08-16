namespace CrestApps.OrchardCore.Payments.Models;

public class PaymentMethod
{
    public string Key { get; set; }

    public string Title { get; set; }

    public bool HasProcessor { get; set; }
}
