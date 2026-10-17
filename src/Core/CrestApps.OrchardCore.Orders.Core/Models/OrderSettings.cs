namespace CrestApps.OrchardCore.Orders.Core.Models;

/// <summary>
/// How the store handles orders, under Settings › Commerce › Orders.
/// </summary>
public sealed class OrderSettings
{
    /// <summary>
    /// Gets or sets whether a paid order's confirmation is emailed to the customer. It is sent only when the site has
    /// an email service.
    /// </summary>
    public bool SendConfirmations { get; set; } = true;
}
