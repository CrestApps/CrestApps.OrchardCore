using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses;
using OrchardCore.ContentManagement;
using OrchardCore.Data;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Addresses.Indexes;

/// <summary>
/// Maps country content items into <see cref="CountryIndex"/> rows using the country part ISO code and the
/// title part display text.
/// </summary>
public sealed class CountryIndexProvider : IndexProvider<ContentItem>, IScopedIndexProvider
{
    /// <summary>
    /// Describes how country content items are projected into <see cref="CountryIndex"/> rows.
    /// </summary>
    /// <param name="context">The YesSql describe context for content items.</param>
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<CountryIndex>()
            .Map(contentItem =>
            {
                if (!string.Equals(contentItem.ContentType, AddressConstants.Country, StringComparison.Ordinal))
                {
                    return null;
                }

                JsonNode content = contentItem.Content;
                var code = content?[AddressConstants.CountryPart]?["Code"]?["Text"]?.GetValue<string>();

                return new CountryIndex
                {
                    ContentItemId = contentItem.ContentItemId,
                    Code = code?.Trim().ToUpperInvariant(),
                    DisplayText = contentItem.DisplayText,
                    Published = contentItem.Published,
                    Latest = contentItem.Latest,
                };
            });
    }
}
