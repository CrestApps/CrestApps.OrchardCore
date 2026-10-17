using CrestApps.OrchardCore.Addresses.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.ContentFields.Fields;

/// <summary>
/// A postal address on a content item: the shared <see cref="Models.Address"/> value, laid out and checked the way
/// its country writes addresses. A content type can have several, such as a Home and a Work address.
/// </summary>
public sealed class AddressField : ContentField
{
    /// <summary>
    /// Gets or sets the address, or <see langword="null"/> when none was entered.
    /// </summary>
    public Address Address { get; set; }
}
