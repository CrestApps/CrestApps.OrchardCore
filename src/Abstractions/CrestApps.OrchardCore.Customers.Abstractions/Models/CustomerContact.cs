namespace CrestApps.OrchardCore.Customers.Models;

/// <summary>
/// The default immutable <see cref="ICustomerContact"/> implementation.
/// </summary>
public sealed class CustomerContact : ICustomerContact
{
    /// <inheritdoc/>
    public string DisplayName { get; init; }

    /// <inheritdoc/>
    public string Email { get; init; }
}
