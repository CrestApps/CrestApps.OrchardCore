using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Api;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Banks;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.DataSources;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Reports;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.DataSources;
using OrchardCore.Security.Permissions;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Surveys.Results;

/// <summary>
/// The Surveys reads that grow with the number of surveys, on a real SQLite store held to SQL Server's limit of 2,100
/// parameters per statement: the data sets, the reports, the grading queue, the survey list, the API list and bank
/// draws never name more ids than one <c>IN</c> list holds, and read surveys from the survey index instead of loading
/// every survey.
/// </summary>
public sealed class SurveyScaleTests : IAsyncLifetime
{
    // More surveys than SQL Server accepts parameters in one statement.
    private const int OwnedSurveys = ParameterLimitedSqliteConnectionFactory.SqlServerLimit + 150;

    private static readonly DateTime _day = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private ResultsSqliteStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _store = await ResultsSqliteStore.CreateAsync();

        await using (var session = _store.OpenSession())
        {
            // pat owns every survey but one, lee owns the last; each survey is published.
            for (var index = 0; index < OwnedSurveys; index++)
            {
                await session.SaveAsync(Survey(SurveyId(index), "pat-id", tags: index < 3 ? ["mine"] : []), false, SurveysConstants.CollectionName, cancellationToken);

                if (index % 500 == 499)
                {
                    await session.SaveChangesAsync(cancellationToken);
                }
            }

            await session.SaveAsync(Survey("other", "lee-id", tags: ["theirs"]), false, SurveysConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Version("v-first", SurveyId(0)), false, SurveysConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Version("v-last", SurveyId(OwnedSurveys - 1)), false, SurveysConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Version("v-other", "other"), false, SurveysConstants.CollectionName, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);

            var first = ResultsSqliteStore.Response("r-first", Version("v-first", SurveyId(0)), SurveyResponseStatus.Completed, _day, ("q1", new SurveyAnswer { Values = ["red"] }));
            var last = ResultsSqliteStore.Response("r-last", Version("v-last", SurveyId(OwnedSurveys - 1)), SurveyResponseStatus.Completed, _day.AddHours(1), ("q1", new SurveyAnswer { Values = ["blue"] }));
            var other = ResultsSqliteStore.Response("r-other", Version("v-other", "other"), SurveyResponseStatus.Completed, _day, ("q1", new SurveyAnswer { Values = ["red"] }));
            last.Grading = GradingStatus.Pending;

            await session.SaveAsync(first, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            await session.SaveAsync(last, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            await session.SaveAsync(other, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        _store.ParameterLimit = ParameterLimitedSqliteConnectionFactory.SqlServerLimit;
    }

    public async ValueTask DisposeAsync()
        => await _store.DisposeAsync();

    [Fact]
    public void SurveyIdChunks_SplitsIntoListsOfAThousandWithoutEmptiesOrRepeats()
    {
        // Arrange
        var ids = Enumerable.Range(0, 2500).Select(index => "s" + index).Concat(["s1", string.Empty, null]).ToList();

        // Act
        var chunks = SurveyIdChunks.Of(ids).ToList();

        // Assert
        Assert.Equal([1000, 1000, 500], chunks.Select(chunk => chunk.Length));
        Assert.Equal(2500, chunks.SelectMany(chunk => chunk).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task SurveyResponses_ForAnOwnerOfMoreSurveysThanOneInListHolds_ReadsOnlyTheirSurveys()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var rows = await ReadAsync(source, "SurveyResponses", TestSecurity.User("pat"));

        // Assert
        Assert.Equal(["r-first", "r-last"], rows.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task SurveyResponses_ForAGlobalReader_ReadsEverySurvey()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var rows = await ReadAsync(source, "SurveyResponses", TestSecurity.User("kim"));

        // Assert
        Assert.Equal(["r-first", "r-last", "r-other"], rows.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task GetDataSetsAsync_ForAnOwnerOfManySurveys_ListsOneWideSetPerSurvey()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var names = (await source.GetDataSetsAsync(new DataSourceContext { User = TestSecurity.User("pat") }, TestContext.Current.CancellationToken))
            .Select(set => set.Name)
            .Where(name => name.StartsWith(SurveyWideDataSetFactory.Prefix, StringComparison.Ordinal))
            .ToList();

        // Assert
        Assert.Equal(OwnedSurveys, names.Count);
        Assert.DoesNotContain(SurveyWideDataSetFactory.Prefix + "other", names);
    }

    [Fact]
    public async Task ReportData_ListReadableAsync_ReadsSummariesOfOwnedSurveysFromTheIndex()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var data = ReportData(session, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var surveys = await data.ListReadableAsync(TestSecurity.User("pat"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OwnedSurveys, surveys.Count);
        Assert.DoesNotContain(surveys, survey => survey.ItemId == "other");

        // Summaries come from the index: the drafts are never loaded.
        Assert.All(surveys, survey => Assert.Empty(survey.Draft.Pages));
    }

    [Fact]
    public async Task ActivityCounts_ForMoreSurveysThanOneInListHolds_CountEachSurvey()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var ids = Enumerable.Range(0, OwnedSurveys).Select(SurveyId).ToList();

        // Act
        var counts = await new SurveyResultsQueries(session).CountActivityAsync(ids, _day.AddDays(-1), _day.AddDays(1), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((1L, 0L), counts[SurveyId(0)]);
        Assert.Equal((1L, 1L), counts[SurveyId(OwnedSurveys - 1)]);
        Assert.False(counts.ContainsKey("other"));
    }

    [Fact]
    public async Task ResponseQueries_ForMoreSurveysThanOneInListHolds_CountsAndPagesAcrossChunks()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var queries = new SurveyResponseQueries(session);
        var ids = Enumerable.Range(0, OwnedSurveys).Select(SurveyId).ToList();

        // Act
        var (count, items) = await queries.ListAsync(new SurveyResponseQuery { SurveyIds = ids, Take = 1 }, TestContext.Current.CancellationToken);
        var (pendingCount, pending) = await queries.ListAsync(new SurveyResponseQuery { SurveyIds = ids, Grading = GradingStatus.Pending, Take = 10 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, count);
        Assert.Equal("r-last", Assert.Single(items).ItemId);
        Assert.Equal(1, pendingCount);
        Assert.Equal("r-last", Assert.Single(pending).ItemId);
    }

    [Fact]
    public async Task R6L16_ExcludedSurveys_AreLeftOutOfNamedSurveys_AndTooManyOfEverySurveyAreRefused()
    {
        // Arrange: more excluded surveys than one NOT IN list holds.
        await using var session = _store.OpenSession();
        var queries = new SurveyResponseQueries(session);
        var ids = Enumerable.Range(0, OwnedSurveys).Select(SurveyId).ToList();
        var excluded = ids.Take(OwnedSurveys - 1).ToList();

        // Act
        var (count, items) = await queries.ListAsync(new SurveyResponseQuery { SurveyIds = ids, ExcludedSurveyIds = excluded, Take = 10 }, TestContext.Current.CancellationToken);
        var none = await queries.ListAsync(new SurveyResponseQuery { SurveyIds = excluded, ExcludedSurveyIds = excluded, Take = 10 }, TestContext.Current.CancellationToken);

        // Assert: named surveys simply aren't named; every survey but more than 1,000 is refused, never sent to SQL.
        Assert.Equal(1, count);
        Assert.Equal("r-last", Assert.Single(items).ItemId);
        Assert.Equal(0, none.Count);
        await Assert.ThrowsAsync<ArgumentException>(() => queries.ListAsync(new SurveyResponseQuery { ExcludedSurveyIds = excluded, Take = 10 }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task R6L16_ResponsesStartedAtTheSameTimeInDifferentChunks_PageWithoutRepeatOrGap()
    {
        // Arrange: two responses with the same start time, one in the first chunk of surveys and one in the last.
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var write = _store.OpenSession())
        {
            var a = ResultsSqliteStore.Response("tie-a", Version("v-last", SurveyId(OwnedSurveys - 1)), SurveyResponseStatus.Completed, _day.AddDays(1));
            var b = ResultsSqliteStore.Response("tie-b", Version("v-first", SurveyId(0)), SurveyResponseStatus.Completed, _day.AddDays(1));
            await write.SaveAsync(a, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            await write.SaveAsync(b, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            await write.SaveChangesAsync(cancellationToken);
        }

        await using var session = _store.OpenSession();
        var queries = new SurveyResponseQueries(session);
        var ids = Enumerable.Range(0, OwnedSurveys).Select(SurveyId).ToList();

        // Act
        var first = await queries.ListAsync(new SurveyResponseQuery { SurveyIds = ids, Take = 1 }, cancellationToken);
        var second = await queries.ListAsync(new SurveyResponseQuery { SurveyIds = ids, Skip = 1, Take = 1 }, cancellationToken);
        var single = await queries.ListAsync(new SurveyResponseQuery { SurveyIds = [SurveyId(0), SurveyId(OwnedSurveys - 1)], Take = 2 }, cancellationToken);

        // Assert: the merge of chunks breaks the tie as one query does (newest first, then by id).
        Assert.Equal("tie-b", Assert.Single(first.Items).ItemId);
        Assert.Equal("tie-a", Assert.Single(second.Items).ItemId);
        Assert.Equal(["tie-b", "tie-a"], single.Items.Select(item => item.ItemId));
    }

    [Fact]
    public async Task AccessScopes_ProviderGrantsByOwner_AreReadFromTheIndex()
    {
        // Arrange
        await using var session = _store.OpenSession();
        await using (var write = _store.OpenSession())
        {
            var owned = Survey("course-quiz", null);
            owned.Owner = new RecordRef(RecordKinds.ContentItem, "item1");
            await write.SaveAsync(owned, false, SurveysConstants.CollectionName, TestContext.Current.CancellationToken);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var reader = new SurveyIndexReader(session, new SurveyStore(session));
        var scopes = new SurveyAccessScopes(TestSecurity.Granting(), [new OwnerGrants(new RecordRef(RecordKinds.ContentItem, "item1"))], reader);

        // Act
        var scope = await scopes.ResolveAsync(TestSecurity.User("sam"), SurveyOperation.ViewResponses, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(scope.All);
        Assert.Equal(["course-quiz"], scope.SurveyIds);
    }

    [Fact]
    public async Task IndexReader_RowIndexedBeforeTheAnonymityColumn_ReadsTheSurveyItself()
    {
        // Arrange
        await using (var write = _store.OpenSession())
        {
            var survey = Survey("old-anonymous", "pat-id");
            survey.Access = new SurveyAccessSettings { Anonymity = AnonymityMode.Anonymous, MinimumGroupSize = 9 };
            await write.SaveAsync(survey, false, SurveysConstants.CollectionName, TestContext.Current.CancellationToken);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await ClearAnonymityColumnAsync("old-anonymous");
        await using var session = _store.OpenSession();
        var reader = new SurveyIndexReader(session, new SurveyStore(session));

        // Act
        var read = Assert.Single(await reader.ListAsync(["old-anonymous"], publishedOnly: false, TestContext.Current.CancellationToken));
        var owned = await reader.ListIdsOwnedByAsync("pat-id", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AnonymityMode.Anonymous, read.Access.Anonymity);
        Assert.Equal(9, read.Access.MinimumGroupSize);
        Assert.Contains("old-anonymous", owned);
    }

    [Fact]
    public async Task P2L5_AnExport_ReadsEachResponseOnce_WhileResponsesAreSavedAgain()
    {
        // Arrange: more responses of one identified survey than one page.
        const int total = SurveysConstants.PageSize * 2 + 30;

        await using (var write = _store.OpenSession())
        {
            for (var index = 0; index < total; index++)
            {
                var response = ResultsSqliteStore.Response("export-" + index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture), Version("v-export", SurveyId(1)), SurveyResponseStatus.Completed, _day.AddMinutes(index), ("q1", new SurveyAnswer { Values = ["red"] }));
                await write.SaveAsync(response, false, SurveysConstants.ResponsesCollectionName, TestContext.Current.CancellationToken);
            }

            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var session = _store.OpenSession();
        var queries = new SurveyResultsQueries(session);
        var read = new List<string>();

        // Act: after the first page, the responses already read are saved again (graded, upkept), which writes their
        // index rows again under new ids.
        await foreach (var response in queries.ReadAsync(new SurveyResultsScope { SurveyId = SurveyId(1) }, TestContext.Current.CancellationToken))
        {
            read.Add(response.ItemId);

            if (read.Count == SurveysConstants.PageSize)
            {
                foreach (var id in read.Take(50))
                {
                    var again = await session.Query<SurveyResponse, SurveyResponseIndex>(index => index.ItemId == id, collection: SurveysConstants.ResponsesCollectionName).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
                    again.ModifiedUtc = _day.AddDays(1);
                    await session.SaveAsync(again, false, SurveysConstants.ResponsesCollectionName, TestContext.Current.CancellationToken);
                }

                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        // Assert
        Assert.Equal(total, read.Count);
        Assert.Equal(total, read.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task VL9_TheGradingQueue_ReadsMoreSurveysWithPendingGradingThanOneInListHolds()
    {
        // Arrange: a response to grade on every survey pat owns, more surveys than SQL Server takes parameters.
        _store.ParameterLimit = null;

        await using (var write = _store.OpenSession())
        {
            for (var index = 0; index < OwnedSurveys; index++)
            {
                var response = ResultsSqliteStore.Response("pending-" + index, Version("v-p" + index, SurveyId(index)), SurveyResponseStatus.Completed, _day, ("q1", new SurveyAnswer { Values = ["red"] }));
                response.Grading = GradingStatus.Pending;
                await write.SaveAsync(response, false, SurveysConstants.ResponsesCollectionName, TestContext.Current.CancellationToken);
            }

            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        _store.ParameterLimit = ParameterLimitedSqliteConnectionFactory.SqlServerLimit;
        await using var session = _store.OpenSession();
        var results = new SurveyResultsQueries(session);
        var queue = new CrestApps.OrchardCore.Surveys.Core.Scoring.SurveyGradingQueue(
            TestSecurity.Granting(SurveysPermissions.GradeSurveyResponses),
            [],
            [],
            new SurveyStore(session),
            new SurveyResponseQueries(session),
            Options.Create(new SurveyContextKindOptions()),
            results,
            new CrestApps.OrchardCore.Surveys.Core.Access.SurveyAnonymousGroups(results, new ServiceCollection().BuildServiceProvider()),
            new SurveyIndexReader(session, new SurveyStore(session)));

        // Act
        var (count, items) = await queue.ListAsync(TestSecurity.User("grader"), null, 0, 10, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OwnedSurveys + 1, count);
        Assert.Equal(10, items.Count);
    }

    [Fact]
    public async Task VL11_TheReindexStep_IndexesALegacyRow_AndNeverOverwritesASurveyChangedMeanwhile()
    {
        // Arrange: two legacy rows.
        await using (var write = _store.OpenSession())
        {
            await write.SaveAsync(Survey("legacy-a", "kim-id"), false, SurveysConstants.CollectionName, TestContext.Current.CancellationToken);
            await write.SaveAsync(Survey("legacy-b", "kim-id"), false, SurveysConstants.CollectionName, TestContext.Current.CancellationToken);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await ClearAnonymityColumnAsync("legacy-a");
        await ClearAnonymityColumnAsync("legacy-b");

        // Act 1: a run whose surveys another save changed after it loaded them (their stored version moved on).
        await using (var racing = _store.OpenSession())
        {
            Assert.True(await new CrestApps.OrchardCore.Surveys.Migrations.SurveysReindexStep(racing, new SurveyStore(racing)).RunAsync(new CrestApps.OrchardCore.YesSql.Core.Upgrades.ResumableStepContext(), TestContext.Current.CancellationToken));

            var transaction = await racing.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var configuration = _store.Store.Configuration;
            var documents = configuration.TableNameConvention.GetDocumentTable(SurveysConstants.CollectionName);
            await transaction.Connection.ExecuteAsync($"UPDATE \"{configuration.TablePrefix}{documents}\" SET \"Version\" = \"Version\" + 1", transaction: transaction);

            // Assert 1: the run's commit fails rather than overwrite them.
            await Assert.ThrowsAsync<ConcurrencyException>(() => racing.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        // Act 2: the next run, with nothing changed meanwhile.
        await using (var run = _store.OpenSession())
        {
            await new CrestApps.OrchardCore.Surveys.Migrations.SurveysReindexStep(run, new SurveyStore(run)).RunAsync(new CrestApps.OrchardCore.YesSql.Core.Upgrades.ResumableStepContext(), TestContext.Current.CancellationToken);
            await run.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Assert 2: both rows are indexed.
        await using var session = _store.OpenSession();
        var owned = await session.QueryIndex<SurveyIndex>(index => index.OwnerId == "kim-id", collection: SurveysConstants.CollectionName).ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["legacy-a", "legacy-b"], owned.Select(row => row.ItemId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task VL10_OwnAndTagLists_IncludeSurveysTheReindexStepHasNotReached()
    {
        // Arrange: a legacy row with no owner (and no tag rows) in the index.
        await using (var write = _store.OpenSession())
        {
            await write.SaveAsync(Survey("legacy-own", "kim-id", tags: ["legacy"]), false, SurveysConstants.CollectionName, TestContext.Current.CancellationToken);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await ClearAnonymityColumnAsync("legacy-own");
        await ClearTagRowsAsync("legacy-own");
        await using var session = _store.OpenSession();
        var reader = new SurveyIndexReader(session, new SurveyStore(session));
        var list = new SurveyListService(session, reader);
        var http = new DefaultHttpContext { User = TestSecurity.User("kim") };

        // Act
        var (ownCount, own) = await list.ListAsync(new SurveyListQuery { OwnerId = "kim-id" }, 0, 10, TestContext.Current.CancellationToken);
        var (taggedCount, _) = await list.ListAsync(new SurveyListQuery { Tag = "legacy" }, 0, 10, TestContext.Current.CancellationToken);
        var tags = await list.ListTagsAsync("kim-id", TestContext.Current.CancellationToken);
        var api = Body(await SurveysApiEndpoints.ListSurveysAsync(0, 10, http, TestSecurity.Granting(SurveysPermissions.ManageOwnSurveys), session, reader));

        // Assert
        Assert.Equal(1, ownCount);
        Assert.Equal("legacy-own", Assert.Single(own).ItemId);
        Assert.Equal(1, taggedCount);
        Assert.Equal(["legacy"], tags);
        Assert.Equal(1, api.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task VL5_TheEmbedPicker_ReadsTheFirstOpenSurveysByTitle_OfAScopeLargerThanOneInList()
    {
        // Arrange: a scope of every survey pat owns, more than one IN list holds.
        await using var session = _store.OpenSession();
        var reader = new SurveyIndexReader(session, new SurveyStore(session));
        var ids = Enumerable.Range(0, OwnedSurveys).Select(SurveyId).ToList();

        // Act
        var first = await reader.ListByTitleAsync(ids, SurveyStatus.Open, 5, TestContext.Current.CancellationToken);
        var closed = await reader.ListByTitleAsync(ids, SurveyStatus.Closed, 5, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ids.Take(5), first.Select(survey => survey.ItemId));
        Assert.Empty(closed);
    }

    [Fact]
    public async Task ListService_FiltersByOwnerAndTagAndPagesInTheDatabase()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var list = new SurveyListService(session, new SurveyIndexReader(session, new SurveyStore(session)));

        // Act
        var (ownCount, ownPage) = await list.ListAsync(new SurveyListQuery { OwnerId = "pat-id" }, 10, 5, TestContext.Current.CancellationToken);
        var (taggedCount, tagged) = await list.ListAsync(new SurveyListQuery { OwnerId = "pat-id", Tag = "mine" }, 0, 50, TestContext.Current.CancellationToken);
        var (theirsCount, _) = await list.ListAsync(new SurveyListQuery { OwnerId = "pat-id", Tag = "theirs" }, 0, 50, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OwnedSurveys, ownCount);
        Assert.Equal(5, ownPage.Count);
        Assert.Equal(3, taggedCount);
        Assert.All(tagged, survey => Assert.Contains("mine", survey.Tags));
        Assert.Equal(0, theirsCount);
    }

    [Fact]
    public async Task ListService_ListTagsAsync_ForAnOwner_ListsOnlyTheirTags()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var list = new SurveyListService(session, new SurveyIndexReader(session, new SurveyStore(session)));

        // Act
        var own = await list.ListTagsAsync("pat-id", TestContext.Current.CancellationToken);
        var all = await list.ListTagsAsync(null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["mine"], own);
        Assert.Equal(["mine", "theirs"], all);
    }

    [Fact]
    public async Task ApiList_WithoutManageSurveys_PagesTheCallersOwnSurveys()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var http = new DefaultHttpContext { User = TestSecurity.User("lee") };

        // Act
        var result = await SurveysApiEndpoints.ListSurveysAsync(0, 10, http, TestSecurity.Granting(SurveysPermissions.ManageOwnSurveys), session, new SurveyIndexReader(session, new SurveyStore(session)));

        // Assert
        var page = Body(result);
        Assert.Equal(1, page.GetProperty("count").GetInt32());
        Assert.Equal("other", Assert.Single(page.GetProperty("items").EnumerateArray()).GetProperty("id").GetString());
    }

    [Fact]
    public async Task ApiList_WithManageSurveys_PagesPastTheFirstPage()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var http = new DefaultHttpContext { User = TestSecurity.User("kim") };
        var authorization = TestSecurity.Granting(SurveysPermissions.ManageSurveys, SurveysPermissions.ManageOwnSurveys);

        // Act
        var last = Body(await SurveysApiEndpoints.ListSurveysAsync(OwnedSurveys, 50, http, authorization, session, new SurveyIndexReader(session, new SurveyStore(session))));

        // Assert
        Assert.Equal(OwnedSurveys + 1, last.GetProperty("count").GetInt32());
        Assert.Single(last.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task BankDraw_ByATagOnMoreQuestionsThanOneInListHolds_TakesTheFirstByIdAndWarns()
    {
        // Arrange
        const int questions = ParameterLimitedSqliteConnectionFactory.SqlServerLimit + 400;
        _store.ParameterLimit = null;

        await using (var write = _store.OpenSession())
        {
            for (var index = 0; index < questions; index++)
            {
                await write.SaveAsync(new BankQuestion
                {
                    ItemId = "bq" + index.ToString("D5", System.Globalization.CultureInfo.InvariantCulture),
                    BankId = "bank1",
                    Tags = ["algebra"],
                    Question = new SurveyQuestion { Name = "q", ControlType = ResultsTestControlHandler.Name, Title = "Q" },
                }, false, SurveysConstants.CollectionName, TestContext.Current.CancellationToken);

                if (index % 500 == 499)
                {
                    await write.SaveChangesAsync(TestContext.Current.CancellationToken);
                }
            }

            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        _store.ParameterLimit = ParameterLimitedSqliteConnectionFactory.SqlServerLimit;

        await using var session = _store.OpenSession();
        var resolver = new SurveyBankDrawResolver(new BankQuestionStore(session), RecipeRoundTrip.Localizer<SurveyBankDrawResolver>());
        var definition = new SurveyDefinition
        {
            Pages =
            [
                new SurveyPage
                {
                    Id = "p1",
                    Name = "page_1",
                    Pools = [new QuestionPool { Id = "pool1", Name = "pool_1", BankDraws = [new BankDraw { BankId = "bank1", Tags = ["algebra"], MaxCandidates = 10 }] }],
                },
            ],
        };

        // Act
        var messages = await resolver.ResolveAsync(definition, TestContext.Current.CancellationToken);
        var (count, page) = await new QuestionBankQueries(session).ListQuestionsAsync(new BankQuestionQuery { BankId = "bank1", Tag = "algebra", Take = 5 }, TestContext.Current.CancellationToken);
        var byIds = await new BankQuestionStore(session).ListByIdsAsync([.. Enumerable.Range(0, questions).Select(index => "bq" + index.ToString("D5", System.Globalization.CultureInfo.InvariantCulture))], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Enumerable.Range(0, 10).Select(index => "bq" + index.ToString("D5", System.Globalization.CultureInfo.InvariantCulture)), definition.Pages[0].Questions.Select(question => question.Id));
        Assert.Contains(messages, message => message.Code == "bank-draw-capped");
        Assert.Equal(questions, count);
        Assert.Equal(5, page.Count);
        Assert.Equal(questions, byIds.Count);
    }

    private async Task ClearAnonymityColumnAsync(string surveyId)
    {
        var configuration = _store.Store.Configuration;
        var table = configuration.TableNameConvention.GetIndexTable(typeof(SurveyIndex), SurveysConstants.CollectionName);
        await using var connection = configuration.ConnectionFactory.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await connection.ExecuteAsync($"UPDATE \"{configuration.TablePrefix}{table}\" SET \"Anonymity\" = NULL, \"OwnerId\" = NULL WHERE \"ItemId\" = @Id", new { Id = surveyId });
    }

    private async Task ClearTagRowsAsync(string surveyId)
    {
        var configuration = _store.Store.Configuration;
        var table = configuration.TableNameConvention.GetIndexTable(typeof(SurveyTagIndex), SurveysConstants.CollectionName);
        await using var connection = configuration.ConnectionFactory.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await connection.ExecuteAsync($"DELETE FROM \"{configuration.TablePrefix}{table}\" WHERE \"SurveyId\" = @Id", new { Id = surveyId });
    }

    private static JsonElement Body(IResult result)
    {
        var ok = Assert.IsAssignableFrom<IValueHttpResult>(result);

        return JsonSerializer.SerializeToElement(ok.Value);
    }

    private static SurveysDataSource Source(ISession session, params Permission[] grants)
    {
        var authorization = TestSecurity.Granting(grants);
        var services = new ServiceCollection().BuildServiceProvider();
        var reader = new SurveyIndexReader(session, new SurveyStore(session));
        var access = new SurveyDataSetAccess(new SurveyAccessScopes(authorization, [], reader), reader, services);
        var registry = new SurveyControlRegistry(Options.Create(new SurveyControlOptions()), [new ResultsTestControlHandler()], [], []);
        var versions = new SurveyVersionStore(session);
        var names = new SurveyRespondentNames(services, RecipeRoundTrip.Localizer<SurveyRespondentNames>());

        ISurveysDataSet[] sets =
        [
            new SurveyResponsesDataSet(session, authorization, access, [], names, versions, new SurveyCollectorStore(session), RecipeRoundTrip.Localizer<SurveyResponsesDataSet>()),
        ];

        return new SurveysDataSource(sets, new SurveyWideDataSetFactory(session, authorization, access, new SurveyStore(session), versions, registry, names, RecipeRoundTrip.Localizer<SurveyWideDataSet>()));
    }

    private static SurveyReportData ReportData(ISession session, params Permission[] grants)
    {
        var authorization = TestSecurity.Granting(grants);
        var services = new ServiceCollection().BuildServiceProvider();
        var reader = new SurveyIndexReader(session, new SurveyStore(session));

        return new SurveyReportData(
            new SurveyStore(session),
            new SurveyVersionStore(session),
            authorization,
            services,
            new FixedOffsetLocalClock(() => _day, TimeSpan.Zero),
            new SurveyRespondentNames(services, RecipeRoundTrip.Localizer<SurveyRespondentNames>()),
            RecipeRoundTrip.Localizer<SurveyReportData>(),
            new SurveyAccessScopes(authorization, [], reader),
            reader);
    }

    private static async Task<IReadOnlyList<string>> ReadAsync(SurveysDataSource source, string dataSet, ClaimsPrincipal user)
    {
        var table = await ReportDesignerTestServices.ReadAsync(source, new DataSourceQuery
        {
            DataSet = dataSet,
            MaxRows = 100,
            Context = new DataSourceContext { User = user },
        }, TestContext.Current.CancellationToken);

        var id = table.Fields.Select(field => field.Name).ToList().IndexOf("Id");

        return table.Rows.Select(row => (string)row[id]).ToList();
    }

    private static string SurveyId(int index)
        => "s" + index.ToString("D5", System.Globalization.CultureInfo.InvariantCulture);

    private static Survey Survey(string id, string ownerId, List<string> tags = null)
        => new()
        {
            ItemId = id,
            Name = id,
            Title = id.ToUpperInvariant(),
            Status = SurveyStatus.Open,
            PublishedVersionNumber = 1,
            OwnerId = ownerId,
            Tags = tags ?? [],
            CreatedUtc = _day,
            Access = new SurveyAccessSettings { Anonymity = AnonymityMode.Identified },
        };

    private static SurveyVersion Version(string id, string surveyId)
        => ResultsSqliteStore.Version(id, surveyId, 1, ResultsSqliteStore.ChoiceQuestion("q1", "color", "red", "blue"));

    private sealed class OwnerGrants : ISurveyAccessProvider
    {
        private readonly RecordRef _owner;

        public OwnerGrants(RecordRef owner)
        {
            _owner = owner;
        }

        public Task<bool> GrantsAsync(ClaimsPrincipal user, Survey survey, SurveyOperation operation, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("A provider that lists its grants is never asked about each survey.");

        public Task<SurveyAccessGrants> ListGrantsAsync(ClaimsPrincipal user, SurveyOperation operation, CancellationToken cancellationToken = default)
            => Task.FromResult(new SurveyAccessGrants { Owners = [_owner] });
    }
}
