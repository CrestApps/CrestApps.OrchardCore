using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.DataSources;
using CrestApps.Core.Infrastructure.Indexing.Models;
using CrestApps.OrchardCore.AI.Core.DataSources;
using CrestApps.OrchardCore.AI.DataSources;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.AI.DataSources;

/// <summary>
/// The per-request result filter extension point of AI Data Sources: every keyed content manager is wrapped, the
/// wrapper asks the index for more chunks and keeps what the filters keep, and with no filter it changes nothing.
/// </summary>
public sealed class DataSourceResultFilterTests
{
    [Fact]
    public async Task WithoutFilters_SearchPassesThroughUnchanged()
    {
        // Arrange
        var inner = new RecordingContentManager(Results(10));
        var manager = new FilteringDataSourceContentManager(inner, [], null, new AIDataSourceResultFilterOptions());

        // Act
        var results = (await manager.SearchAsync(new IndexInfo(), [1f], "ds", 3, "f", TestContext.Current.CancellationToken)).ToList();

        // Assert
        Assert.Equal(3, inner.LastTopN);
        Assert.Equal("f", inner.LastFilter);
        Assert.Equal(10, results.Count);
    }

    [Fact]
    public async Task WithFilters_AsksForMoreAndReturnsAtMostTopNOfWhatTheFiltersKeep()
    {
        // Arrange
        var inner = new RecordingContentManager(Results(20));
        var manager = new FilteringDataSourceContentManager(inner, [new DropOddFilter()], null, new AIDataSourceResultFilterOptions { OverfetchFactor = 4, MaxCandidates = 200 });

        // Act
        var results = (await manager.SearchAsync(new IndexInfo(), [1f], "ds", 3, cancellationToken: TestContext.Current.CancellationToken)).ToList();

        // Assert
        Assert.Equal(12, inner.LastTopN);
        Assert.Equal(["r0", "r2", "r4"], results.Select(result => result.ReferenceId));
    }

    [Fact]
    public async Task TrySearch_FiltersSuccessfulOutcomesAndKeepsFailuresAndUnknowns()
    {
        // Arrange
        var filters = new IAIDataSourceResultFilter[] { new DropOddFilter() };
        var succeeded = new FilteringDataSourceContentManager(new RecordingContentManager(Results(6)), filters, null, new AIDataSourceResultFilterOptions());
        var failed = new FilteringDataSourceContentManager(new RecordingContentManager(Results(6)) { Outcome = DataSourceSearchOutcome.Failure() }, filters, null, new AIDataSourceResultFilterOptions());
        var unknown = new FilteringDataSourceContentManager(new RecordingContentManager(Results(6)) { ReturnNullOutcome = true }, filters, null, new AIDataSourceResultFilterOptions());

        // Act
        var kept = await succeeded.TrySearchAsync(new IndexInfo(), [1f], "ds", 2, cancellationToken: TestContext.Current.CancellationToken);
        var failure = await failed.TrySearchAsync(new IndexInfo(), [1f], "ds", 2, cancellationToken: TestContext.Current.CancellationToken);
        var none = await unknown.TrySearchAsync(new IndexInfo(), [1f], "ds", 2, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(kept.Succeeded);
        Assert.Equal(["r0", "r2"], kept.Results.Select(result => result.ReferenceId));
        Assert.False(failure.Succeeded);
        Assert.Null(none);
    }

    [Theory]
    [InlineData(5, 4, 200, 20)]
    [InlineData(100, 4, 200, 200)]
    [InlineData(300, 4, 200, 300)]
    [InlineData(5, 0, 200, 5)]
    public void CandidateCount_IsTopNTimesTheFactorWithinTheCapAndNeverBelowTopN(int topN, int factor, int cap, int expected)
    {
        var options = new AIDataSourceResultFilterOptions { OverfetchFactor = factor, MaxCandidates = cap };

        Assert.Equal(expected, options.GetCandidateCount(topN));
    }

    [Fact]
    public async Task Decorate_WrapsEveryKeyedContentManagerOnceAndKeepsItsKey()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddKeyedScoped<IDataSourceContentManager>("A", (_, _) => new RecordingContentManager(Results(4)));
        services.AddKeyedScoped<IDataSourceContentManager, FourResultsContentManager>("B");
        services.AddKeyedSingleton<IDataSourceContentManager>("C", new RecordingContentManager(Results(4)));
        services.AddAIDataSourceResultFilter<DropOddFilter>();

        // Act
        services.DecorateDataSourceContentManagers();
        services.DecorateDataSourceContentManagers();

        // Assert
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var a = Assert.IsType<FilteringDataSourceContentManager>(scope.ServiceProvider.GetRequiredKeyedService<IDataSourceContentManager>("A"));
        var b = Assert.IsType<FilteringDataSourceContentManager>(scope.ServiceProvider.GetRequiredKeyedService<IDataSourceContentManager>("B"));
        var c = Assert.IsType<FilteringDataSourceContentManager>(scope.ServiceProvider.GetRequiredKeyedService<IDataSourceContentManager>("C"));
        Assert.IsType<RecordingContentManager>(a.Inner);
        Assert.IsType<FourResultsContentManager>(b.Inner);
        Assert.IsType<RecordingContentManager>(c.Inner);
        Assert.Equal(["r0", "r2"], (await a.SearchAsync(new IndexInfo(), [1f], "ds", 5, cancellationToken: TestContext.Current.CancellationToken)).Select(result => result.ReferenceId));
    }

    [Fact]
    public void AIDataSources_RunsTheDecorationAfterTheProviderStartups()
    {
        Assert.True(new ResultFiltersStartup().Order > 0);
    }

    private static List<DataSourceSearchResult> Results(int count)
        => Enumerable.Range(0, count).Select(index => new DataSourceSearchResult { ReferenceId = "r" + index, Score = 1f - (index / 100f) }).ToList();

    private sealed class DropOddFilter : IAIDataSourceResultFilter
    {
        public Task<IReadOnlyList<DataSourceSearchResult>> FilterAsync(AIDataSourceResultFilterContext context, IReadOnlyList<DataSourceSearchResult> results)
            => Task.FromResult<IReadOnlyList<DataSourceSearchResult>>(results.Where(result => int.Parse(result.ReferenceId[1..], System.Globalization.CultureInfo.InvariantCulture) % 2 == 0).ToList());
    }

    private sealed class IndexInfo : IIndexProfileInfo
    {
        public string IndexProfileId => "p";

        public string IndexName => "kb";

        public string ProviderName => "Test";

        public string IndexFullName => "kb";
    }

    private sealed class FourResultsContentManager : IDataSourceContentManager
    {
        private readonly RecordingContentManager _inner = new(Results(4));

        public Task<IEnumerable<DataSourceSearchResult>> SearchAsync(IIndexProfileInfo indexProfile, float[] embedding, string dataSourceId, int topN, string filter = null, CancellationToken cancellationToken = default)
            => _inner.SearchAsync(indexProfile, embedding, dataSourceId, topN, filter, cancellationToken);

        public Task<long> DeleteByDataSourceIdAsync(IIndexProfileInfo indexProfile, string dataSourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(0L);
    }

    private sealed class RecordingContentManager : IDataSourceContentManager
    {
        private readonly List<DataSourceSearchResult> _results;

        public RecordingContentManager(IEnumerable<DataSourceSearchResult> results)
        {
            _results = results.ToList();
        }

        public int LastTopN { get; private set; }

        public string LastFilter { get; private set; }

        public DataSourceSearchOutcome Outcome { get; init; }

        public bool ReturnNullOutcome { get; init; }

        public Task<IEnumerable<DataSourceSearchResult>> SearchAsync(IIndexProfileInfo indexProfile, float[] embedding, string dataSourceId, int topN, string filter = null, CancellationToken cancellationToken = default)
        {
            LastTopN = topN;
            LastFilter = filter;

            return Task.FromResult<IEnumerable<DataSourceSearchResult>>(_results);
        }

        public Task<DataSourceSearchOutcome> TrySearchAsync(IIndexProfileInfo indexProfile, float[] embedding, string dataSourceId, int topN, string filter = null, CancellationToken cancellationToken = default)
        {
            LastTopN = topN;

            if (ReturnNullOutcome)
            {
                return Task.FromResult<DataSourceSearchOutcome>(null);
            }

            return Task.FromResult(Outcome ?? DataSourceSearchOutcome.Success(_results));
        }

        public Task<long> DeleteByDataSourceIdAsync(IIndexProfileInfo indexProfile, string dataSourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(0L);
    }
}
