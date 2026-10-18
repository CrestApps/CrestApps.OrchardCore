using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Customers.Core.Models;

/// <summary>
/// An address in a customer's address book: the shared <see cref="Models.Address"/> value, with an id and the
/// customer's name for it. It can be edited and deleted; an order keeps its own copy, made with
/// <see cref="ToAddress"/> when it is placed, so a change here never changes an order.
/// </summary>
public sealed class CustomerAddress
{
    public string AddressId { get; set; }

    /// <summary>
    /// Gets or sets the customer's own name for the address, such as Home or Office.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the address itself. Replace it to change the address; the value is immutable.
    /// </summary>
    public Address Address { get; set; }

    /// <summary>
    /// The copy an order keeps.
    /// </summary>
    public Address ToAddress()
        => Address?.Clone() ?? new Address();
}
