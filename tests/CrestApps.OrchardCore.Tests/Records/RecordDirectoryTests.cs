using System.Security.Claims;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Records;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Tests.Records;

/// <summary>
/// A record reference is an open kind and an id; each module that keeps records of a kind registers a source, and the
/// directory finds and describes them without knowing the module, for the records the user may search.
/// </summary>
public sealed class RecordDirectoryTests
{
    private static readonly ClaimsPrincipal _user = new(new ClaimsIdentity("test"));

    [Fact]
    public async Task TheDirectory_DescribesRecordsOfEveryKind_WithOneLookupPerKind()
    {
        // Arrange
        var orders = new Source("Order", ("o1", "Order 1001"), ("o2", "Order 1002"));
        var assets = new Source("Asset", ("a1", "Forklift"));
        var directory = new RecordDirectory([orders, assets, new Source("Order", ("o1", "Shadowed"))]);

        // Act
        var summaries = await directory.GetSummariesAsync(
            [new RecordRef("Order", "o1"), new RecordRef("order", "o2"), new RecordRef("Asset", "a1"), new RecordRef("Invoice", "i1"), default],
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["Order", "Asset"], directory.Sources.Select(source => source.Kind));
        Assert.Equal("Order 1001", summaries[new RecordRef("Order", "o1")].Title);
        Assert.Equal("Order 1002", summaries[new RecordRef("order", "o2")].Title);
        Assert.Equal("Forklift", summaries[new RecordRef("Asset", "a1")].Title);
        Assert.Equal(3, summaries.Count);
        Assert.Equal(1, orders.Lookups);
        Assert.Same(orders, directory.GetSource("ORDER"));
        Assert.Null(await directory.GetSummaryAsync(new RecordRef("Invoice", "i1"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ASearch_ReturnsNothing_WhenTheUserMayNotSearchTheKind()
    {
        // Arrange
        var orders = new Source("Order", ("o1", "Order 1001")) { Searchable = false };
        var assets = new Source("Asset", ("a1", "Forklift"));
        var directory = new RecordDirectory([orders, assets]);

        // Act
        var forbidden = await directory.SearchAsync("Order", new RecordSearch { Text = "Order", User = _user }, TestContext.Current.CancellationToken);
        var allowed = await directory.SearchAsync("Asset", new RecordSearch { Text = "fork", User = _user }, TestContext.Current.CancellationToken);
        var anonymous = await directory.SearchAsync("Asset", new RecordSearch { Text = "fork" }, TestContext.Current.CancellationToken);
        var unknown = await directory.SearchAsync("Invoice", new RecordSearch { User = _user }, TestContext.Current.CancellationToken);
        var searchable = await directory.GetSearchableSourcesAsync(_user, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(forbidden);
        Assert.Equal(0, orders.Searches);
        Assert.Equal("Forklift", Assert.Single(allowed).Title);
        Assert.Empty(anonymous);
        Assert.Empty(unknown);
        Assert.Same(assets, Assert.Single(searchable));
        Assert.Empty(await directory.GetSearchableSourcesAsync(null, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("Order:abc", "Order", "abc")]
    [InlineData("Asset:4x:7", "Asset", "4x:7")]
    public void ARecordRef_RoundTripsAsKindAndId(string text, string kind, string id)
    {
        Assert.True(RecordRef.TryParse(text, out var record));
        Assert.Equal(new RecordRef(kind, id), record);
        Assert.Equal(text, record.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Order")]
    [InlineData(":abc")]
    [InlineData("Order:")]
    public void ARecordRef_WithoutAKindOrAnId_IsNotRead(string text)
    {
        Assert.False(RecordRef.TryParse(text, out var record));
        Assert.True(record.IsEmpty);
        Assert.Equal(string.Empty, record.ToString());
    }

    [Fact]
    public void ARecordRef_IsStoredAsText_AndReadFromTextOrAnObject()
    {
        // Arrange
        var holder = new Holder { Subject = new RecordRef("Order", "o1") };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(holder);
        var fromText = System.Text.Json.JsonSerializer.Deserialize<Holder>(json);
        var fromObject = System.Text.Json.JsonSerializer.Deserialize<Holder>("""{"Subject":{"kind":"Asset","id":"a1","extra":[1,2]}}""");
        var empty = System.Text.Json.JsonSerializer.Deserialize<Holder>(System.Text.Json.JsonSerializer.Serialize(new Holder()));

        // Assert
        Assert.Equal("""{"Subject":"Order:o1"}""", json);
        Assert.Equal(new RecordRef("Order", "o1"), fromText.Subject);
        Assert.Equal(new RecordRef("Asset", "a1"), fromObject.Subject);
        Assert.True(empty.Subject.IsEmpty);
    }

    private sealed class Holder
    {
        public RecordRef Subject { get; set; }
    }

    private sealed class Source : IRecordSource
    {
        private readonly Dictionary<string, string> _titles;

        public Source(string kind, params (string Id, string Title)[] records)
        {
            Kind = kind;
            _titles = records.ToDictionary(record => record.Id, record => record.Title);
        }

        public string Kind { get; }

        [JsonIgnore]
        public LocalizationSource DisplayName => LocalizationSource.Create<RecordDirectoryTests>("Record");

        public bool Searchable { get; set; } = true;

        public int Lookups { get; private set; }

        public int Searches { get; private set; }

        public Task<bool> CanSearchAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
            => Task.FromResult(Searchable);

        public Task<IReadOnlyList<RecordSummary>> SearchAsync(RecordSearch search, CancellationToken cancellationToken = default)
        {
            Searches++;

            IReadOnlyList<RecordSummary> found = _titles
                .Where(pair => string.IsNullOrEmpty(search.Text) || pair.Value.Contains(search.Text, StringComparison.OrdinalIgnoreCase))
                .Take(search.Count)
                .Select(pair => new RecordSummary { Id = pair.Key, Title = pair.Value })
                .ToList();

            return Task.FromResult(found);
        }

        public Task<IReadOnlyDictionary<string, RecordSummary>> GetSummariesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
        {
            Lookups++;

            IReadOnlyDictionary<string, RecordSummary> found = ids
                .Where(_titles.ContainsKey)
                .ToDictionary(id => id, id => new RecordSummary { Id = id, Title = _titles[id] });

            return Task.FromResult(found);
        }
    }
}
