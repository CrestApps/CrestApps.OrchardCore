using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Search;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Util;
using OrchardCore.Indexing.Models;
using OrchardCore.Lucene;
using OrchardCore.Lucene.Core;

namespace CrestApps.OrchardCore.Products.Lucene.Services;

/// <summary>
/// Searches a Lucene product search index.
/// </summary>
/// <remarks>
/// Lucene has no facets here, so filter counts and range bounds are gathered while the matching products are
/// read: each facet runs the query without its own filter and counts the facet tokens of every match.
/// </remarks>
public sealed class LuceneProductSearchService : IProductIndexSearchService
{
    private readonly ILuceneIndexStore _indexStore;

    public LuceneProductSearchService(ILuceneIndexStore indexStore)
    {
        _indexStore = indexStore;
    }

    public async Task<ProductSearchResult> SearchAsync(IndexProfile indexProfile, ProductSearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);
        ArgumentNullException.ThrowIfNull(query);

        var indexQuery = ProductSearchIndexQuery.Create(query);
        ProductSearchResult result = null;

        // The callback runs only when the index exists.
        await _indexStore.SearchAsync(indexProfile, searcher =>
        {
            result = Search(searcher, indexQuery);

            return Task.CompletedTask;
        });

        return result ?? throw new InvalidOperationException($"The Lucene index '{indexProfile.IndexName}' does not exist.");
    }

    private static ProductSearchResult Search(IndexSearcher searcher, ProductSearchIndexQuery indexQuery)
    {
        var main = BuildQuery(indexQuery, excludedFacetKey: null);
        var sort = new Sort(indexQuery.Sort.Select(ToSortField).ToArray());
        var top = searcher.Search(main, null, indexQuery.Skip + indexQuery.Take, sort);

        var ids = top.ScoreDocs
            .Skip(indexQuery.Skip)
            .Select(hit => searcher.Doc(hit.Doc).Get(ProductSearchIndexFields.ContentItemId))
            .Where(id => !string.IsNullOrEmpty(id))
            .ToArray();

        var results = new Dictionary<string, ProductFacetResult>(StringComparer.Ordinal);

        foreach (var facet in indexQuery.Facets)
        {
            // A facet's counts ignore its own selection, so the other options say how many products they add.
            var facetQuery = indexQuery.HasFilterFor(facet.Key)
                ? BuildQuery(indexQuery, facet.Key)
                : main;

            if (facet.IsRange)
            {
                var range = new RangeCollector(ProductSearchIndexFields.SortNumber(facet.AttributeKey), ProductSearchIndexFields.MaxNumber(facet.AttributeKey));

                searcher.Search(facetQuery, range);

                results[facet.Key] = new ProductFacetResult
                {
                    Key = facet.Key,
                    Min = range.Min is { } min ? (decimal)min : null,
                    Max = range.Max is { } max ? (decimal)max : null,
                };

                continue;
            }

            var counter = new TokenCountCollector(ProductSearchIndexFields.Facets(facet.AttributeKey));

            searcher.Search(facetQuery, counter);

            results[facet.Key] = ProductSearchIndexFields.ToFacetResult(facet, counter.GetCounts());
        }

        return new ProductSearchResult
        {
            ContentItemIds = ids,
            Total = top.TotalHits,
            Facets = results,
        };
    }

    internal static Query BuildQuery(ProductSearchIndexQuery indexQuery, string excludedFacetKey)
    {
        var root = new BooleanQuery();

        AddAnyOf(root, ProductSearchIndexFields.ContentType, indexQuery.ContentTypes);
        AddAnyOf(root, ProductSearchIndexFields.Culture, indexQuery.Cultures);

        if (indexQuery.Keyword is not null)
        {
            root.Add(KeywordQuery(indexQuery.Keyword), Occur.MUST);
        }

        var filters = excludedFacetKey is null ? indexQuery.Filters : indexQuery.FiltersExcept(excludedFacetKey);

        foreach (var filter in filters)
        {
            if (filter.IsRange)
            {
                root.Add(NumericRangeQuery.NewDoubleRange(filter.Field, (double?)filter.Min, (double?)filter.Max, true, true), Occur.MUST);
            }
            else
            {
                AddAnyOf(root, filter.Field, filter.Values);
            }
        }

        return root.Clauses.Count == 0 ? new MatchAllDocsQuery() : root;
    }

    private static void AddAnyOf(BooleanQuery root, string field, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        var any = new BooleanQuery();

        foreach (var value in values)
        {
            any.Add(new TermQuery(new Term(field, value)), Occur.SHOULD);
        }

        root.Add(any, Occur.MUST);
    }

    // Every word must start a word of the title, SKU or a searchable value, so a partly typed word matches too;
    // or the keyword is the SKU exactly.
    private static BooleanQuery KeywordQuery(string keyword)
    {
        var any = new BooleanQuery
        {
            { new TermQuery(new Term(ProductSearchIndexFields.Sku, keyword)), Occur.SHOULD },
        };

        var words = Analyze(keyword);

        if (words.Count > 0)
        {
            var all = new BooleanQuery();

            foreach (var word in words)
            {
                all.Add(new PrefixQuery(new Term(ProductSearchIndexFields.Keywords, word)), Occur.MUST);
            }

            any.Add(all, Occur.SHOULD);
        }

        return any;
    }

    // The words the index's analyzer (Orchard Core's default, the standard analyzer) makes of the keyword.
    private static List<string> Analyze(string text)
    {
        var words = new List<string>();

        using var analyzer = new StandardAnalyzer(LuceneConstants.DefaultVersion);
        using var stream = analyzer.GetTokenStream(ProductSearchIndexFields.Keywords, text);

        var term = stream.AddAttribute<ICharTermAttribute>();

        stream.Reset();

        while (stream.IncrementToken())
        {
            words.Add(term.ToString());
        }

        stream.End();

        return words;
    }

    internal static SortField ToSortField(ProductIndexSortField sort)
    {
        // Products without a value sort last in either direction.
        if (IsNumber(sort.Field))
        {
            var number = new SortField(sort.Field, FieldCache.NUMERIC_UTILS_DOUBLE_PARSER, sort.Descending);
            number.SetMissingValue(sort.Descending ? double.MinValue : double.MaxValue);

            return number;
        }

        var text = new SortField(sort.Field, SortFieldType.STRING, sort.Descending);
        text.SetMissingValue(sort.Descending ? SortField.STRING_FIRST : SortField.STRING_LAST);

        return text;
    }

    private static bool IsNumber(string field)
        => field is ProductSearchIndexFields.Price or ProductSearchIndexFields.Featured or ProductSearchIndexFields.Position or ProductSearchIndexFields.Sequence ||
           field.EndsWith(ProductSearchIndexFields.SortNumberSuffix, StringComparison.Ordinal) ||
           field.EndsWith(ProductSearchIndexFields.MaxNumberSuffix, StringComparison.Ordinal);

    /// <summary>
    /// Counts the facet tokens of the matching products, a segment at a time.
    /// </summary>
    private sealed class TokenCountCollector : ICollector
    {
        private readonly string _field;
        private readonly Dictionary<string, long> _counts = new(StringComparer.Ordinal);

        private SortedSetDocValues _values;
        private long[] _ordinalCounts;

        public TokenCountCollector(string field)
        {
            _field = field;
        }

        public bool AcceptsDocsOutOfOrder => true;

        public void SetScorer(Scorer scorer)
        {
        }

        public void SetNextReader(AtomicReaderContext context)
        {
            Flush();

            _values = FieldCache.DEFAULT.GetDocTermOrds(context.AtomicReader, _field);
            _ordinalCounts = new long[_values.ValueCount];
        }

        public void Collect(int doc)
        {
            _values.SetDocument(doc);

            long ordinal;

            while ((ordinal = _values.NextOrd()) != SortedSetDocValues.NO_MORE_ORDS)
            {
                _ordinalCounts[ordinal]++;
            }
        }

        public Dictionary<string, long> GetCounts()
        {
            Flush();

            return _counts;
        }

        private void Flush()
        {
            if (_values is null)
            {
                return;
            }

            var bytes = new BytesRef();

            for (var ordinal = 0; ordinal < _ordinalCounts.Length; ordinal++)
            {
                if (_ordinalCounts[ordinal] == 0)
                {
                    continue;
                }

                _values.LookupOrd(ordinal, bytes);

                var token = bytes.Utf8ToString();

                _counts[token] = _counts.GetValueOrDefault(token) + _ordinalCounts[ordinal];
            }

            _values = null;
            _ordinalCounts = null;
        }
    }

    /// <summary>
    /// The smallest and largest number of an attribute among the matching products.
    /// </summary>
    private sealed class RangeCollector : ICollector
    {
        private readonly string _minField;
        private readonly string _maxField;

        private FieldCache.Doubles _mins;
        private FieldCache.Doubles _maxes;
        private IBits _hasMin;
        private IBits _hasMax;

        public RangeCollector(string minField, string maxField)
        {
            _minField = minField;
            _maxField = maxField;
        }

        public double? Min { get; private set; }

        public double? Max { get; private set; }

        public bool AcceptsDocsOutOfOrder => true;

        public void SetScorer(Scorer scorer)
        {
        }

        public void SetNextReader(AtomicReaderContext context)
        {
            var reader = context.AtomicReader;

            _mins = FieldCache.DEFAULT.GetDoubles(reader, _minField, FieldCache.NUMERIC_UTILS_DOUBLE_PARSER, true);
            _maxes = FieldCache.DEFAULT.GetDoubles(reader, _maxField, FieldCache.NUMERIC_UTILS_DOUBLE_PARSER, true);
            _hasMin = FieldCache.DEFAULT.GetDocsWithField(reader, _minField);
            _hasMax = FieldCache.DEFAULT.GetDocsWithField(reader, _maxField);
        }

        public void Collect(int doc)
        {
            if (_hasMin.Get(doc))
            {
                var value = _mins.Get(doc);
                Min = Min is null ? value : Math.Min(Min.Value, value);
            }

            if (_hasMax.Get(doc))
            {
                var value = _maxes.Get(doc);
                Max = Max is null ? value : Math.Max(Max.Value, value);
            }
        }
    }
}
