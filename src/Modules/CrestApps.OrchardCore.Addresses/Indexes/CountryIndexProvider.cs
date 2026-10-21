using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses;
using OrchardCore.ContentManagement;
using OrchardCore.Data;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Addresses.Indexes;

/// <summary>
/// Maps every Country content item into a <see cref="CountryIndex"/> row with its ISO code in capitals.
/// </summary>
public sealed class CountryIndexProvider : IndexProvider<ContentItem>, IScopedIndexProvider
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<CountryIndex>()
            .When(contentItem => string.Equals(contentItem.ContentType, AddressConstants.Country, StringComparison.Ordinal))
            .Map(contentItem =>
            {
                JsonNode content = contentItem.Content;
                var code = content?[AddressConstants.CountryPart]?[AddressConstants.CodeField]?["Text"]?.GetValue<string>()?.Trim();

                return new CountryIndex
                {
                    ContentItemId = contentItem.ContentItemId,
                    Code = code?.ToUpperInvariant(),
                    DisplayText = contentItem.DisplayText,
                    Published = contentItem.Published,
                    Latest = contentItem.Latest,
                };
            });
    }
}
