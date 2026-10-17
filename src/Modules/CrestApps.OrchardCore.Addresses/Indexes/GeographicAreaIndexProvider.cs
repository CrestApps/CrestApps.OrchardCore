using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses;
using OrchardCore.ContentManagement;
using OrchardCore.Data;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Addresses.Indexes;

/// <summary>
/// Maps every Country content item into a <see cref="GeographicAreaIndex"/> row with its ISO code in capitals. The
/// Region, County, City and District items of sites that used earlier 3.0 previews are indexed too, with their parent,
/// so the obsolete <c>IAddressResolver</c> keeps working for them.
/// </summary>
public sealed class GeographicAreaIndexProvider : IndexProvider<ContentItem>, IScopedIndexProvider
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<GeographicAreaIndex>()
            .Map(contentItem =>
            {
                JsonNode content = contentItem.Content;

                if (string.Equals(contentItem.ContentType, AddressConstants.Country, StringComparison.Ordinal))
                {
                    var code = content?[AddressConstants.CountryPart]?[AddressConstants.CodeField]?["Text"]?.GetValue<string>()?.Trim();

                    return new GeographicAreaIndex
                    {
                        ContentItemId = contentItem.ContentItemId,
                        ContentType = contentItem.ContentType,
                        Code = code?.ToUpperInvariant(),
                        DisplayText = contentItem.DisplayText,
                        Published = contentItem.Published,
                        Latest = contentItem.Latest,
                    };
                }

                if (!_legacyPlaces.TryGetValue(contentItem.ContentType, out var place))
                {
                    return null;
                }

                var part = content?[place.PartName];

                return new GeographicAreaIndex
                {
                    ContentItemId = contentItem.ContentItemId,
                    ContentType = contentItem.ContentType,
                    Code = part?[AddressConstants.CodeField]?["Text"]?.GetValue<string>()?.Trim(),
                    ParentContentItemId = part?[place.ParentField]?["ContentItemIds"] is JsonArray ids ? ids.FirstOrDefault()?.GetValue<string>() : null,
                    DisplayText = contentItem.DisplayText,
                    Published = contentItem.Published,
                    Latest = contentItem.Latest,
                };
            });
    }

#pragma warning disable CS0618 // The places of earlier 3.0 previews stay indexed until they are removed.
    private static readonly Dictionary<string, (string PartName, string ParentField)> _legacyPlaces = new(StringComparer.Ordinal)
    {
        [AddressConstants.Region] = (AddressConstants.RegionPart, "Country"),
        [AddressConstants.County] = (AddressConstants.CountyPart, "Region"),
        [AddressConstants.City] = (AddressConstants.CityPart, "Region"),
        [AddressConstants.District] = (AddressConstants.DistrictPart, "City"),
    };
#pragma warning restore CS0618
}
