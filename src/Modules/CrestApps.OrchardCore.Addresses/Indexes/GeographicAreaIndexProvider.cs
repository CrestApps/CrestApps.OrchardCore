using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses;
using OrchardCore.ContentManagement;
using OrchardCore.Data;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Addresses.Indexes;

/// <summary>
/// Maps every geographic content item (country, region, county, city, and district) into a
/// <see cref="GeographicAreaIndex"/> row, extracting the money-safe code and the parent reference from the
/// type's information part.
/// </summary>
public sealed class GeographicAreaIndexProvider : IndexProvider<ContentItem>, IScopedIndexProvider
{
    /// <summary>
    /// Describes how geographic content items are projected into <see cref="GeographicAreaIndex"/> rows.
    /// </summary>
    /// <param name="context">The YesSql describe context for content items.</param>
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<GeographicAreaIndex>()
            .Map(contentItem =>
            {
                if (!TryGetMetadata(contentItem.ContentType, out var partName, out var parentField))
                {
                    return null;
                }

                JsonNode content = contentItem.Content;
                var part = content?[partName];

                var code = part?[AddressConstants.CodeField]?["Text"]?.GetValue<string>()?.Trim();

                if (string.Equals(contentItem.ContentType, AddressConstants.Country, StringComparison.Ordinal))
                {
                    code = code?.ToUpperInvariant();
                }

                string parentContentItemId = null;

                if (parentField is not null && part?[parentField]?["ContentItemIds"] is JsonArray ids)
                {
                    parentContentItemId = ids.FirstOrDefault()?.GetValue<string>();
                }

                return new GeographicAreaIndex
                {
                    ContentItemId = contentItem.ContentItemId,
                    ContentType = contentItem.ContentType,
                    Code = code,
                    ParentContentItemId = parentContentItemId,
                    DisplayText = contentItem.DisplayText,
                    Published = contentItem.Published,
                    Latest = contentItem.Latest,
                };
            });
    }

    private static bool TryGetMetadata(string contentType, out string partName, out string parentField)
    {
        switch (contentType)
        {
            case AddressConstants.Country:
                partName = AddressConstants.CountryPart;
                parentField = null;

                return true;
            case AddressConstants.Region:
                partName = AddressConstants.RegionPart;
                parentField = "Country";

                return true;
            case AddressConstants.County:
                partName = AddressConstants.CountyPart;
                parentField = "Region";

                return true;
            case AddressConstants.City:
                partName = AddressConstants.CityPart;
                parentField = "Region";

                return true;
            case AddressConstants.District:
                partName = AddressConstants.DistrictPart;
                parentField = "City";

                return true;
            default:
                partName = null;
                parentField = null;

                return false;
        }
    }
}
