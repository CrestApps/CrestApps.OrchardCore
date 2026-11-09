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
        : this(customer, userId, CustomerPrivacyAction.Erased)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerPrivacyContext"/> class.
    /// </summary>
    /// <param name="customer">The customer.</param>
    /// <param name="userId">The account the customer belonged to, or <see langword="null"/> for a guest.</param>
    /// <param name="action">Why the customer's data is handled: an admin's erasure, a retention expiry or an export.</param>
    public CustomerPrivacyContext(Customer customer, string userId, CustomerPrivacyAction action)
    {
        Customer = customer;
        UserId = userId;
        Action = action;
    }

    public Customer Customer { get; }

    /// <summary>
    /// Gets why the customer's data is handled: <see cref="CustomerPrivacyAction.Erased"/> for an explicit erasure,
    /// <see cref="CustomerPrivacyAction.Expired"/> when the guest retention window ran out, or
    /// <see cref="CustomerPrivacyAction.Exported"/> for an export. A handler that reaches records beyond commerce only
    /// does so for an explicit erasure.
    /// </summary>
    public CustomerPrivacyAction Action { get; }

    /// <summary>
    /// Gets the account the customer belonged to, or <see langword="null"/> for a guest.
    /// </summary>
    public string UserId { get; }
}

public sealed class CustomerExportContext : CustomerPrivacyContext
{
    public CustomerExportContext(Customer customer, JsonObject export)
        : base(customer, customer.UserId, CustomerPrivacyAction.Exported)
    {
        Export = export;
    }

    /// <summary>
    /// Gets the export, where each handler adds its own section.
    /// </summary>
    public JsonObject Export { get; }
}
