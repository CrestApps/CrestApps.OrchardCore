using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Customers.Core.Models;

namespace CrestApps.OrchardCore.Customers.Core.Services;

/// <summary>
/// Erases and exports a customer's personal data, for access and erasure requests and the retention window.
/// </summary>
public interface ICustomerPrivacyService
{
    /// <summary>
    /// Erases the customer's personal data now, and the personal details of their orders, and records who did it.
    /// </summary>
    Task EraseAsync(Customer customer, string by, CancellationToken cancellationToken = default);

    /// <summary>
    /// Erases a guest's personal data once the retention window has passed.
    /// </summary>
    Task ExpireAsync(Customer customer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Everything the store holds on the customer, and records who asked.
    /// </summary>
    Task<JsonObject> ExportAsync(Customer customer, string by, CancellationToken cancellationToken = default);
}
