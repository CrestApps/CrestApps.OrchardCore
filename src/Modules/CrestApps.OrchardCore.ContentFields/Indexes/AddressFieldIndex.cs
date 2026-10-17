using OrchardCore.ContentFields.Indexing.SQL;

namespace CrestApps.OrchardCore.ContentFields.Indexes;

/// <summary>
/// Indexes every <c>AddressField</c> by place, so content can be listed by country, region, city or postal code.
/// </summary>
public sealed class AddressFieldIndex : ContentFieldIndex
{
    public string Country { get; set; }

    public string Region { get; set; }

    public string City { get; set; }

    public string PostalCode { get; set; }
}
