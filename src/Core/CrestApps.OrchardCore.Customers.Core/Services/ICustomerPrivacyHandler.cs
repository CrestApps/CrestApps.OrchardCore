using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Customers.Core.Models;

namespace CrestApps.OrchardCore.Customers.Core.Services;

/// <summary>
/// Takes part in erasing and exporting a customer: removes the personal details other modules keep about them, such as
/// on orders, and adds what they hold to an export.
/// </summary>
public interface ICustomerPrivacyHandler
{
    /// <summary>
    /// Gets where the handler runs among the others; a lower value runs first, and handlers with the same value run in
    /// the order they were registered. A handler that finds a customer's records through details another handler
    /// erases, such as the email on a checkout, runs first.
    /// </summary>
    int Order => 0;

    Task ErasingAsync(CustomerPrivacyContext context, CancellationToken cancellationToken = default);

    Task ExportingAsync(CustomerExportContext context, CancellationToken cancellationToken = default);
}

public class CustomerPrivacyContext
{
    public CustomerPrivacyContext(Customer customer, string userId)
    {
        Customer = customer;
        UserId = userId;
    }

    public Customer Customer { get; }

    /// <summary>
    /// Gets the account the customer belonged to, or <see langword="null"/> for a guest.
    /// </summary>
    public string UserId { get; }
}

public sealed class CustomerExportContext : CustomerPrivacyContext
{
    public CustomerExportContext(Customer customer, JsonObject export)
        : base(customer, customer.UserId)
    {
        Export = export;
    }

    /// <summary>
    /// Gets the export, where each handler adds its own section.
    /// </summary>
    public JsonObject Export { get; }
}
