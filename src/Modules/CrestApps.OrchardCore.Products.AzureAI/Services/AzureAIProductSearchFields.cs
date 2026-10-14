using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Products.Core.Search;
using OrchardCore.AzureAI.Models;
using OrchardCore.Indexing;

namespace CrestApps.OrchardCore.Products.AzureAI.Services;

/// <summary>
/// How the product search fields are declared and queried in Azure AI Search.
/// </summary>
/// <remarks>
/// Azure AI Search declares every field up front and Orchard Core cannot declare a list of numbers there, so ranges
/// and their bounds read an attribute's smallest and largest numbers, which are exact for an attribute with one
/// number, such as a year or a price. It also sorts empty values first in ascending order, so each sortable field
/// has a companion that is 1 when it has a value, sorted first.
/// </remarks>
public static class AzureAIProductSearchFields
{
    public const string HasValueSuffix = "_has";

    private const string Delimiters = "|,;~^`";

    private static readonly char[] _specialCharacters = ['+', '-', '&', '|', '!', '(', ')', '{', '}', '[', ']', '^', '"', '~', '*', '?', ':', '\\', '/'];

    /// <summary>
    /// The companion field that is 1 when the field has a value.
    /// </summary>
    public static string HasValue(string field)
        => field + HasValueSuffix;

    public static bool IsSortable(string field)
        => field is ProductSearchIndexFields.Price or ProductSearchIndexFields.Featured or ProductSearchIndexFields.Position or
            ProductSearchIndexFields.PublishedUtc or ProductSearchIndexFields.Sequence or ProductSearchIndexFields.TitleSort ||
           field.EndsWith(ProductSearchIndexFields.SortNumberSuffix, StringComparison.Ordinal) ||
           field.EndsWith(ProductSearchIndexFields.MaxNumberSuffix, StringComparison.Ordinal) ||
           field.EndsWith(ProductSearchIndexFields.SortTextSuffix, StringComparison.Ordinal);

    /// <summary>
    /// The fields of a product search index, for the given attribute keys.
    /// </summary>
    public static IEnumerable<AzureAISearchIndexMap> GetMappings(IEnumerable<string> attributeKeys)
    {
        foreach (var definition in ProductSearchIndexFields.GetDefinitions(attributeKeys))
        {
            // A list of numbers cannot be declared; ranges read the smallest and largest numbers instead.
            if (definition.Kind == ProductIndexFieldKind.Number && definition.IsCollection)
            {
                continue;
            }

            var map = new AzureAISearchIndexMap(definition.Name, ToType(definition.Kind))
            {
                IndexingKey = definition.Name,
            };

            switch (definition.Kind)
            {
                case ProductIndexFieldKind.Keyword when definition.IsCollection:
                    // Only a searchable text field can be a collection in Orchard Core's schema. Keyword search
                    // names its own fields, so these values are filtered and counted, not searched.
                    map.IsCollection = true;
                    map.IsSearchable = true;
                    map.IsFilterable = true;
                    map.IsFacetable = true;
                    break;

                case ProductIndexFieldKind.Keyword:
                    map.IsKey = definition.IsKey;
                    map.IsFilterable = true;
                    map.IsSortable = definition.IsSortable;
                    break;

                case ProductIndexFieldKind.Text:
                    map.IsSearchable = true;
                    break;

                default:
                    map.IsFilterable = true;
                    map.IsSortable = definition.IsSortable;
                    break;
            }

            yield return map;

            if (IsSortable(definition.Name))
            {
                yield return new AzureAISearchIndexMap(HasValue(definition.Name), DocumentIndex.Types.Number)
                {
                    IndexingKey = HasValue(definition.Name),
                    IsSortable = true,
                };
            }
        }
    }

    /// <summary>
    /// The search text of a keyword: every word must start a word of the title or a searchable value.
    /// </summary>
    public static string GetSearchText(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return "*";
        }

        var words = keyword.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => Escape(word) + "*");

        return string.Join(' ', words);
    }

    /// <summary>
    /// The OData filter of a query, without the filter of the given facet.
    /// </summary>
    public static string GetFilter(ProductSearchIndexQuery indexQuery, string excludedFacetKey = null, string requiredField = null)
    {
        ArgumentNullException.ThrowIfNull(indexQuery);

        var conditions = new List<string>();

        if (indexQuery.ContentTypes.Count > 0)
        {
            conditions.Add(In(ProductSearchIndexFields.ContentType, indexQuery.ContentTypes));
        }

        if (indexQuery.Cultures.Count > 0)
        {
            conditions.Add(In(ProductSearchIndexFields.Culture, indexQuery.Cultures));
        }

        var filters = excludedFacetKey is null ? indexQuery.Filters : indexQuery.FiltersExcept(excludedFacetKey);

        foreach (var filter in filters)
        {
            if (filter.IsRange)
            {
                // The product has a number in the range when its numbers reach the minimum and start below the maximum.
                if (filter.Min is { } min)
                {
                    conditions.Add($"{ProductSearchIndexFields.MaxNumber(filter.AttributeKey)} ge {Number(min)}");
                }

                if (filter.Max is { } max)
                {
                    conditions.Add($"{ProductSearchIndexFields.SortNumber(filter.AttributeKey)} le {Number(max)}");
                }

                continue;
            }

            conditions.Add($"{filter.Field}/any(v: {In("v", filter.Values)})");
        }

        if (requiredField is not null)
        {
            conditions.Add($"{requiredField} ne null");
        }

        return conditions.Count == 0 ? null : string.Join(" and ", conditions.Select(condition => $"({condition})"));
    }

    /// <summary>
    /// The order of a sort. Products without a value sort last in either direction.
    /// </summary>
    public static IEnumerable<string> GetOrderBy(IEnumerable<ProductIndexSortField> sort)
    {
        foreach (var field in sort)
        {
            if (!field.Descending)
            {
                yield return $"{HasValue(field.Field)} desc";
            }

            yield return $"{field.Field} {(field.Descending ? "desc" : "asc")}";
        }
    }

    private static string In(string field, IReadOnlyList<string> values)
    {
        // search.in takes the values as one string, split by a character none of them contains.
        var delimiter = Delimiters.FirstOrDefault(candidate => values.All(value => !value.Contains(candidate, StringComparison.Ordinal)));

        if (delimiter == default)
        {
            return string.Join(" or ", values.Select(value => $"{field} eq {Literal(value)}"));
        }

        return $"search.in({field}, {Literal(string.Join(delimiter, values))}, {Literal(delimiter.ToString())})";
    }

    private static string Literal(string value)
        => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private static string Number(decimal value)
        => value.ToString(CultureInfo.InvariantCulture);

    private static string Escape(string word)
    {
        var builder = new StringBuilder(word.Length);

        foreach (var character in word)
        {
            if (_specialCharacters.Contains(character))
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static DocumentIndex.Types ToType(ProductIndexFieldKind kind)
        => kind switch
        {
            ProductIndexFieldKind.Number => DocumentIndex.Types.Number,
            ProductIndexFieldKind.Date => DocumentIndex.Types.DateTime,
            _ => DocumentIndex.Types.Text,
        };
}
