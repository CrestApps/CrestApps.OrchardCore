namespace CrestApps.OrchardCore.Orders.ViewModels;

/// <summary>
/// The page an account made from an order opens from its emailed link, to choose its password.
/// </summary>
public class OrderAccountActivateViewModel
{
    public string UserId { get; set; }

    public string Code { get; set; }

    public string Password { get; set; }

    public string ConfirmPassword { get; set; }
}