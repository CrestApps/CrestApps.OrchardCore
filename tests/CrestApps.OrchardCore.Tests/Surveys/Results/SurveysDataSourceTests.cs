using System.Security.Claims;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.DataSources;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Reports;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DataSources;
using OrchardCore.Security.Permissions;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Surveys.Results;

/// <summary>
/// The Surveys data source on a real SQLite store: which data sets a person sees, which rows they read, and when the
/// respondent is shown.
/// </summary>
public sealed class SurveysDataSourceTests : IAsyncLifetime
{
    private static readonly DateTime _day = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private ResultsSqliteStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _store = await ResultsSqliteStore.CreateAsync();

        var identified = Version("v1", "survey1");
        var anonymous = Version("v2", "survey2");

        await using var session = _store.OpenSession();
        await session.SaveAsync(Survey("survey1", "csat", "v1", "pat-id", AnonymityMode.Identified), false, SurveysConstants.CollectionName, cancellationToken);
        await session.SaveAsync(Survey("survey2", "pulse", "v2", "lee-id", AnonymityMode.Anonymous), false, SurveysConstants.CollectionName, cancellationToken);
        await session.SaveAsync(identified, false, SurveysConstants.CollectionName, cancellationToken);
        await session.SaveAsync(anonymous, false, SurveysConstants.CollectionName, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        var first = ResultsSqliteStore.Response("r1", identified, SurveyResponseStatus.Completed, _day, ("q1", new SurveyAnswer { Values = ["red"] }));
        first.UserId = "u1";
        first.Number = "R-r1";
        var second = ResultsSqliteStore.Response("r2", anonymous, SurveyResponseStatus.Completed, _day, ("q1", new SurveyAnswer { Values = ["blue"] }));
        second.Number = "R-r2";

        // Even a user id left on an anonymous response is never shown.
        second.UserId = "u2";

        await session.SaveAsync(first, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
        await session.SaveAsync(second, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    // Adds completed responses to the anonymous survey, so it reaches its minimum group size of 5.
    private async Task AddAnonymousResponsesAsync(int count)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var anonymous = Version("v2", "survey2");

        await using var session = _store.OpenSession();

        for (var index = 0; index < count; index++)
        {
            var response = ResultsSqliteStore.Response($"a{index}", anonymous, SurveyResponseStatus.Completed, _day, ("q1", new SurveyAnswer { Values = ["red"] }));
            response.Context = new CrestApps.OrchardCore.Records.RecordRef("LearningEnrollment", "e" + index);
            await session.SaveAsync(response, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
        => await _store.DisposeAsync();

    [Fact]
    public async Task GetDataSetsAsync_ForAViewer_ListsTheStaticSetsAndOnePerPublishedSurvey()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var names = (await source.GetDataSetsAsync(new DataSourceContext { User = TestSecurity.User("kim") }, TestContext.Current.CancellationToken)).Select(set => set.Name).ToList();

        // Assert
        Assert.Contains("SurveyResponses", names);
        Assert.Contains("SurveyAnswers", names);
        Assert.Contains("SurveyResponses_csat", names);
        Assert.Contains("SurveyResponses_pulse", names);
    }

    [Fact]
    public async Task GetDataSetsAsync_WithoutAUser_ListsNothing()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var sets = await source.GetDataSetsAsync(new DataSourceContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(sets);
    }

    [Fact]
    public async Task SurveyResponses_ShowsTheRespondentOfIdentifiedSurveys_WithoutExportPermission()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var rows = await ReadAsync(source, "SurveyResponses", TestSecurity.User("kim"));

        // Assert
        Assert.Equal("Deleted user", rows["r1"]["UserName"]);
    }

    [Fact]
    public async Task SurveyResponses_AnAnonymousSurveyBelowItsMinimumGroupSize_ShowsNoRow()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var rows = await ReadAsync(source, "SurveyResponses", TestSecurity.User("kim"));
        var answers = await ReadAsync(source, "SurveyAnswers", TestSecurity.User("kim"), "ResponseId");
        var wide = await ReadAsync(source, "SurveyResponses_pulse", TestSecurity.User("kim"));

        // Assert
        Assert.Equal(["r1"], rows.Keys);
        Assert.Equal(["r1"], answers.Keys);
        Assert.Empty(wide);
    }

    [Fact]
    public async Task SurveyResponses_AnAnonymousSurveyAtItsMinimumGroupSize_ShowsRowsWithDaysOnlyAndNobody()
    {
        // Arrange
        await AddAnonymousResponsesAsync(4);
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var rows = await ReadAsync(source, "SurveyResponses", TestSecurity.User("kim"));
        var wide = await ReadAsync(source, "SurveyResponses_pulse", TestSecurity.User("kim"));

        // Assert
        Assert.Equal(6, rows.Count);
        Assert.Null(rows["r2"]["UserName"]);
        // 4 enrollment responses and 1 without a context: each group is below 5, together they reach it (R6-M3).
        Assert.Equal("Other", rows["a0"]["ContextKind"]);
        Assert.Null(rows["a0"]["ContextId"]);
        Assert.Equal(_day.Date, Assert.IsType<DateTime>(rows["r2"]["CompletedUtc"]));
        Assert.Equal(_day, Assert.IsType<DateTime>(rows["r1"]["CompletedUtc"]));
        Assert.Equal(5, wide.Count);
        Assert.All(wide.Values, row => Assert.Equal("Anonymous", row["Respondent"]));
    }

    [Fact]
    public async Task SurveyResponses_AMaskedRowWithNoContextOrCollector_PrintsOtherNotNone()
    {
        // Arrange: R8-M5, 4 enrollment responses (a0 with no collector) and r2, which has no context: each group is below
        // 5, together they reach it, so all five are masked.
        var cancellationToken = TestContext.Current.CancellationToken;
        await AddAnonymousResponsesAsync(4);

        await using (var write = _store.OpenSession())
        {
            var a0 = await write.Query<SurveyResponse, CrestApps.OrchardCore.Surveys.Core.Indexes.SurveyResponseIndex>(index => index.ItemId == "a0", collection: SurveysConstants.ResponsesCollectionName).FirstOrDefaultAsync(cancellationToken);
            a0.CollectorId = null;
            await write.SaveAsync(a0, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            await write.SaveChangesAsync(cancellationToken);
        }

        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var rows = await ReadAsync(source, "SurveyResponses", TestSecurity.User("kim"));

        // Assert: r2's missing context and a0's missing collector print "Other" like the other masked rows, not their real
        // "none", which would set them apart.
        Assert.Equal("Other", rows["r2"]["ContextKind"]);
        Assert.Equal("Other", rows["a0"]["Collector"]);
        Assert.Equal("Other", rows["a1"]["Collector"]);
        Assert.Null(rows["r2"]["VersionNumber"]);
    }

    [Fact]
    public async Task SurveyPageVisits_OfAnAnonymousSurvey_AreInWholeMinutes()
    {
        // Arrange: R8-Mi17, five anonymous responses (the minimum) whose page visit lasted 2 minutes 41 seconds, and the
        // identified response, whose visit lasted the same.
        var cancellationToken = TestContext.Current.CancellationToken;
        await AddAnonymousResponsesAsync(4);

        await using (var write = _store.OpenSession())
        {
            foreach (var response in await write.Query<SurveyResponse, CrestApps.OrchardCore.Surveys.Core.Indexes.SurveyResponseIndex>(collection: SurveysConstants.ResponsesCollectionName).ListAsync(cancellationToken))
            {
                response.PageVisits = [new SurveyPageVisit { PageId = "p1", EnteredUtc = _day.AddMinutes(-10), LeftUtc = _day.AddMinutes(-10).AddSeconds(161) }];
                await write.SaveAsync(response, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            }

            await write.SaveChangesAsync(cancellationToken);
        }

        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var rows = await ReadAsync(source, "SurveyPageVisits", TestSecurity.User("kim"), "ResponseId");

        // Assert: the anonymous visits round to 3 minutes, so the seconds can't line one up with a time recorded
        // elsewhere; the identified visit keeps its 161 seconds.
        Assert.Equal(161L, rows["r1"]["Seconds"]);
        Assert.Equal(180L, rows["a0"]["Seconds"]);
        Assert.Equal(180L, rows["r2"]["Seconds"]);
    }

    [Fact]
    public async Task SurveyResponses_AnAnonymousSurveyPaddedWithInProgressAndTestResponses_StillShowsNoRow()
    {
        // Arrange: one completed response more, then in-progress, abandoned and test responses well past the minimum of 5.
        var cancellationToken = TestContext.Current.CancellationToken;
        var anonymous = Version("v2", "survey2");

        await using (var write = _store.OpenSession())
        {
            await write.SaveAsync(ResultsSqliteStore.Response("c1", anonymous, SurveyResponseStatus.Completed, _day, ("q1", new SurveyAnswer { Values = ["red"] })), false, SurveysConstants.ResponsesCollectionName, cancellationToken);

            for (var index = 0; index < 4; index++)
            {
                await write.SaveAsync(ResultsSqliteStore.Response($"p{index}", anonymous, SurveyResponseStatus.InProgress, _day, ("q1", new SurveyAnswer { Values = ["red"] })), false, SurveysConstants.ResponsesCollectionName, cancellationToken);
                await write.SaveAsync(ResultsSqliteStore.Response($"x{index}", anonymous, SurveyResponseStatus.Abandoned, _day, ("q1", new SurveyAnswer { Values = ["red"] })), false, SurveysConstants.ResponsesCollectionName, cancellationToken);

                var test = ResultsSqliteStore.Response($"t{index}", anonymous, SurveyResponseStatus.Completed, _day, ("q1", new SurveyAnswer { Values = ["red"] }));
                test.IsTest = true;
                await write.SaveAsync(test, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            }

            await write.SaveChangesAsync(cancellationToken);
        }

        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var rows = await ReadAsync(source, "SurveyResponses", TestSecurity.User("kim"));
        var wide = await ReadAsync(source, "SurveyResponses_pulse", TestSecurity.User("kim"));

        // Assert: two completed, non-test responses never reach the group of 5, whatever else is read with them.
        Assert.Equal(["r1"], rows.Keys);
        Assert.Empty(wide);
    }

    [Fact]
    public async Task WideDataSet_AnAnonymousSurveyFilteredToOnePersonsAnswer_ShowsNoRow()
    {
        // Arrange: 6 completed responses pass the minimum of 5, but only one answered blue.
        await AddAnonymousResponsesAsync(5);
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);
        var blue = new DataCondition { Field = "color", Operator = DataFilterOperator.In, Values = ["blue"] };

        // Act
        var all = await ReadAsync(source, "SurveyResponses_pulse", TestSecurity.User("kim"));
        var filtered = await ReadAsync(source, "SurveyResponses_pulse", TestSecurity.User("kim"), conditions: [blue]);

        // Assert: the filter narrows the survey to one person, so the minimum applies after it. (r2, the one response
        // without a context next to five enrollments, is left out of both: R6-M3.)
        Assert.Equal(5, all.Count);
        Assert.Empty(filtered);
    }

    [Fact]
    public async Task SurveyResponses_AnAnonymousSurveyAtItsMinimum_LeavesOutAStatusTooFewShare()
    {
        // Arrange: 6 completed responses and one abandoned one.
        var cancellationToken = TestContext.Current.CancellationToken;
        await AddAnonymousResponsesAsync(5);

        await using (var write = _store.OpenSession())
        {
            await write.SaveAsync(ResultsSqliteStore.Response("gave-up", Version("v2", "survey2"), SurveyResponseStatus.Abandoned, _day, ("q1", new SurveyAnswer { Values = ["red"] })), false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            await write.SaveChangesAsync(cancellationToken);
        }

        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);
        var abandoned = new DataCondition { Field = "Status", Operator = DataFilterOperator.In, Values = [nameof(SurveyResponseStatus.Abandoned)] };

        // Act
        var rows = await ReadAsync(source, "SurveyResponses", TestSecurity.User("kim"));
        var filtered = await ReadAsync(source, "SurveyResponses", TestSecurity.User("kim"), conditions: [abandoned]);
        var wide = await ReadAsync(source, "SurveyResponses_pulse", TestSecurity.User("kim"));

        // Assert: the lone abandoned response is left out, read with every status or filtered to. (So is r2, the one
        // completed response without a context next to five enrollments: R6-M3.)
        Assert.DoesNotContain("gave-up", rows.Keys);
        Assert.Equal(6, rows.Count);
        Assert.DoesNotContain("gave-up", filtered.Keys);
        Assert.DoesNotContain("gave-up", wide.Keys);
        Assert.Equal(5, wide.Count);
    }

    [Fact]
    public async Task SurveyResponses_AnAnonymousSurvey_ShowsNoSiteWideResponseNumber()
    {
        // Arrange
        await AddAnonymousResponsesAsync(4);
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var rows = await ReadAsync(source, "SurveyResponses", TestSecurity.User("kim"));
        var wide = await ReadAsync(source, "SurveyResponses_pulse", TestSecurity.User("kim"));

        // Assert
        Assert.Equal("R-r1", rows["r1"]["Number"]);
        Assert.NotEqual("R-r2", rows["r2"]["Number"]);
        Assert.False(((string)rows["r2"]["Number"] ?? string.Empty).StartsWith("r2", StringComparison.Ordinal));
        Assert.False(((string)wide["a0"]["Number"] ?? string.Empty).StartsWith("a0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SurveyResponses_WithOnlyOwnSurveys_ReadsOnlyTheirSurveys()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var rows = await ReadAsync(source, "SurveyResponses", TestSecurity.User("pat"));

        // Assert
        Assert.Equal(["r1"], rows.Keys);
    }

    [Fact]
    public async Task WideDataSet_HasAFieldPerQuestion()
    {
        // Arrange
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var schema = await source.GetSchemaAsync("SurveyResponses_csat", new DataSourceContext { User = TestSecurity.User("kim") }, TestContext.Current.CancellationToken);
        var rows = await ReadAsync(source, "SurveyResponses_csat", TestSecurity.User("kim"));

        // Assert
        Assert.NotNull(schema.FindField("color"));
        Assert.Equal("RED", Assert.Single(rows).Value["color"]);
    }

    [Fact]
    public async Task SurveyAnswers_HasOneRowPerAnswerValue_WithItsChoice()
    {
        // Arrange
        await AddAnonymousResponsesAsync(4);
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var rows = await ReadAsync(source, "SurveyAnswers", TestSecurity.User("kim"), "ResponseId");

        // Assert
        Assert.Equal("red", rows["r1"]["ChoiceValue"]);
        Assert.Equal("BLUE", rows["r2"]["ChoiceText"]);
    }

    [Fact]
    public async Task SurveyResponses_AnAnonymousSurvey_ADateFilterWithATimeOfDay_SelectsWholeDays()
    {
        // Arrange: R6-M2, six completed anonymous responses on one day, one of them at 12:07.
        var cancellationToken = TestContext.Current.CancellationToken;
        await AddAnonymousResponsesAsync(4);

        await using (var write = _store.OpenSession())
        {
            await write.SaveAsync(ResultsSqliteStore.Response("late", Version("v2", "survey2"), SurveyResponseStatus.Completed, _day.AddMinutes(7), ("q1", new SurveyAnswer { Values = ["red"] })), false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            await write.SaveChangesAsync(cancellationToken);
        }

        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);
        var anonymousIds = new[] { "a0", "a1", "a2", "a3", "late", "r2" };

        // Act: step the start of the range past most of the day's responses.
        var from1205 = await ReadIdsAsync(source, "SurveyResponses", new DataCondition { Field = "ModifiedUtc", Operator = DataFilterOperator.GreaterThanOrEqual, Values = [_day.AddMinutes(5)] });
        var from1208 = await ReadIdsAsync(source, "SurveyResponses", new DataCondition { Field = "ModifiedUtc", Operator = DataFilterOperator.GreaterThanOrEqual, Values = [_day.AddMinutes(8)] });

        // Assert: the range reaches the index as whole days, so either filter gives the whole day's group, never the one
        // response of 12:07 on its own (which, below the minimum, would show nothing and give its time away).
        Assert.Equal(anonymousIds, from1205.Where(anonymousIds.Contains).Order(StringComparer.Ordinal));
        Assert.Equal(anonymousIds, from1208.Where(anonymousIds.Contains).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task SurveyResponses_AnIncrementalReadPastAnIdentifiedRowOfTheSameDay_KeepsTheAnonymousRows()
    {
        // Arrange: R6-L15, an identified response at 18:00 is the largest change time a pipeline read; the anonymous
        // responses changed earlier that day.
        var cancellationToken = TestContext.Current.CancellationToken;
        await AddAnonymousResponsesAsync(4);

        await using (var write = _store.OpenSession())
        {
            await write.SaveAsync(ResultsSqliteStore.Response("evening", Version("v1", "survey1"), SurveyResponseStatus.Completed, _day.AddHours(6), ("q1", new SurveyAnswer { Values = ["red"] })), false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            await write.SaveChangesAsync(cancellationToken);
        }

        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);
        var first = await ReadAsync(source, "SurveyResponses", TestSecurity.User("kim"));
        var watermark = first.Values.Max(row => Assert.IsType<DateTime>(row["ModifiedUtc"]));

        // Act: the next run keeps the rows changed after the largest change time an earlier run read (18:00, had the
        // anonymous rows been read by their exact times); the pipeline filters each row again on that condition.
        var after = _day.AddHours(6);
        var next = (await ReadAsync(source, "SurveyResponses", TestSecurity.User("kim"), conditions: [new DataCondition { Field = "ModifiedUtc", Operator = DataFilterOperator.GreaterThan, Values = [after] }]))
            .Where(row => Assert.IsType<DateTime>(row.Value["ModifiedUtc"]) > after)
            .Select(row => row.Key)
            .ToList();

        // Assert: an anonymous row's change time is the end of its day (when it was released), so it is the largest one
        // read, and a read past the identified 18:00 row still finds the anonymous rows of that day.
        Assert.Equal(_day.Date.AddDays(1), watermark);
        Assert.Equal(_day.Date.AddDays(1), Assert.IsType<DateTime>(first["a0"]["ModifiedUtc"]));
        Assert.Contains("a0", next);
        Assert.Contains("r2", next);
        Assert.DoesNotContain("evening", next);
    }

    [Fact]
    public async Task SurveyResponses_TodaysAnonymousResponses_WaitUntilTheDayEnds()
    {
        // Arrange: five anonymous responses completed now, besides five older ones.
        var cancellationToken = TestContext.Current.CancellationToken;
        await AddAnonymousResponsesAsync(5);

        await using (var write = _store.OpenSession())
        {
            for (var index = 0; index < 5; index++)
            {
                var now = ResultsSqliteStore.Response($"now{index}", Version("v2", "survey2"), SurveyResponseStatus.Completed, DateTime.UtcNow, ("q1", new SurveyAnswer { Values = ["red"] }));
                now.Context = new CrestApps.OrchardCore.Records.RecordRef("LearningEnrollment", "n" + index);
                await write.SaveAsync(now, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            }

            await write.SaveChangesAsync(cancellationToken);
        }

        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var ids = await ReadIdsAsync(source, "SurveyResponses");

        // Assert
        Assert.Contains("a0", ids);
        Assert.DoesNotContain(ids, id => id.StartsWith("now", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SurveyResponses_AnonymousRows_ComeAfterTheReadInIdOrder_NotInTimeOrder()
    {
        // Arrange: R6-M4, anonymous responses whose ids run against their change times.
        var cancellationToken = TestContext.Current.CancellationToken;
        var anonymous = Version("v2", "survey2");

        await using (var write = _store.OpenSession())
        {
            foreach (var (id, minutes) in new[] { ("z1", -40), ("b2", -10), ("m3", -30), ("c4", -20) })
            {
                await write.SaveAsync(ResultsSqliteStore.Response(id, anonymous, SurveyResponseStatus.Completed, _day.AddMinutes(minutes), ("q1", new SurveyAnswer { Values = ["red"] })), false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            }

            await write.SaveChangesAsync(cancellationToken);
        }

        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var ids = await ReadIdsAsync(source, "SurveyResponses");

        // Assert: the identified row first, then the anonymous survey's rows by id.
        Assert.Equal(["r1", "b2", "c4", "m3", "r2", "z1"], ids);
    }

    [Fact]
    public async Task SurveyResponses_ALinkSentToOnePerson_DoesNotShowTheirRow_NorAnAttemptNumber()
    {
        // Arrange: R6-M3, five responses on the shared link (r2, a0..a3, a4) and one on a link "for Bob".
        var cancellationToken = TestContext.Current.CancellationToken;
        await AddAnonymousResponsesAsync(5);

        await using (var write = _store.OpenSession())
        {
            var bob = ResultsSqliteStore.Response("bob", Version("v2", "survey2"), SurveyResponseStatus.Completed, _day, ("q1", new SurveyAnswer { Values = ["blue"] }));
            bob.CollectorId = "for-bob";
            bob.AttemptNumber = 3;
            await write.SaveAsync(bob, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            await write.SaveChangesAsync(cancellationToken);
        }

        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var rows = await ReadAsync(source, "SurveyResponses", TestSecurity.User("kim"));
        var wide = await ReadAsync(source, "SurveyResponses_pulse", TestSecurity.User("kim"));

        // Assert: "Other" would be Bob alone, so his row isn't listed anywhere (nor is r2, the one response without a
        // context next to five enrollments); the others carry no attempt number.
        Assert.DoesNotContain("bob", rows.Keys);
        Assert.DoesNotContain("bob", wide.Keys);
        Assert.Equal(["a0", "a1", "a2", "a3", "a4"], wide.Keys.Order(StringComparer.Ordinal));
        Assert.Null(rows["a0"]["Attempt"]);
        Assert.Equal(1L, rows["a0"]["VersionNumber"]);
    }

    [Fact]
    public async Task SurveyResponses_AnIncrementalReadWithFewerThanTheMinimumNewRows_StillGetsThem()
    {
        // Arrange: R7-M4, four anonymous responses changed the day before r2 (the minimum is 5), so a pipeline that last
        // read up to the end of that day finds only one new row (r2) on its next run.
        await AddPlainAnonymousAsync("p", 4, _day.AddDays(-1));
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);
        var watermark = _day.Date;

        // Act: the watermark condition (change time after the last run) is passed down, as Orchard's DataSourceStep does.
        var next = (await ReadAsync(source, "SurveyResponses", TestSecurity.User("kim"), conditions: [new DataCondition { Field = "ModifiedUtc", Operator = DataFilterOperator.GreaterThan, Values = [watermark] }]))
            .Where(row => Assert.IsType<DateTime>(row.Value["ModifiedUtc"]) > watermark)
            .Select(row => row.Key)
            .ToList();

        // Assert: the group is counted on the whole survey, not on the one new row, so r2 is released; the four earlier
        // rows, never released before (the survey was below its minimum), read the moment the survey was released.
        Assert.Contains("r2", next);
        Assert.Contains("p0", next);
    }

    [Fact]
    public async Task SurveyResponses_AOneDayFilterOnModified_FindsTheAnonymousRowsShownOnThatDay()
    {
        // Arrange: R7-M4, five anonymous responses changed on 10 November, shown as changed at the end of that day.
        await AddPlainAnonymousAsync("p", 4, _day);
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);
        var shownDay = _day.Date.AddDays(1);

        // Act: "Modified on 11 November".
        var ids = await ReadIdsAsync(
            source,
            "SurveyResponses",
            new DataCondition { Field = "ModifiedUtc", Operator = DataFilterOperator.GreaterThanOrEqual, Values = [shownDay] },
            new DataCondition { Field = "ModifiedUtc", Operator = DataFilterOperator.LessThanOrEqual, Values = [shownDay.AddDays(1).AddSeconds(-1)] });

        // Assert: the raw range read starts a day earlier, so the rows shown on that day are read.
        Assert.Equal(["p0", "p1", "p2", "p3", "r2"], ids.Where(id => id != "r1").Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task SurveyResponses_ACappedRead_StillIncludesAnonymousRows()
    {
        // Arrange: R7-M4, ten identified responses and five released anonymous ones.
        var cancellationToken = TestContext.Current.CancellationToken;
        await AddPlainAnonymousAsync("p", 4, _day);

        await using (var write = _store.OpenSession())
        {
            for (var index = 0; index < 10; index++)
            {
                await write.SaveAsync(ResultsSqliteStore.Response($"n{index}", Version("v1", "survey1"), SurveyResponseStatus.Completed, _day.AddHours(1), ("q1", new SurveyAnswer { Values = ["red"] })), false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            }

            await write.SaveChangesAsync(cancellationToken);
        }

        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act: a preview of four rows.
        var ids = await ReadIdsAsync(source, "SurveyResponses", 4);

        // Assert: the anonymous rows are written one for one between the others, not after all of them.
        Assert.Contains(ids, id => id.StartsWith('p') || id == "r2");
    }

    [Fact]
    public async Task SurveyResponses_AViewerTimeZoneOtherThanTheSites_ReadsTheSitesDays()
    {
        // Arrange: R7-M1, the site is at UTC+14 and the viewer picked UTC-12 for themselves.
        await AddPlainAnonymousAsync("p", 4, _day);
        await using var session = _store.OpenSession();
        var source = Source(session, Zones("Pacific/Kiritimati", "Etc/GMT+12"), SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var rows = await ReadAsync(source, "SurveyResponses", TestSecurity.User("kim"));

        // Assert: 12:00 UTC on 10 November is 02:00 on 11 November at the site, whose day starts at 10:00 UTC on the 10th
        // and ends at 10:00 UTC on the 11th; the viewer's own zone would give 12:00 UTC.
        Assert.Equal(new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc), Assert.IsType<DateTime>(rows["p0"]["StartedUtc"]));
        Assert.Equal(new DateTime(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc), Assert.IsType<DateTime>(rows["p0"]["ModifiedUtc"]));
    }

    [Fact]
    public async Task SurveyResponses_ACollectorAndAVersionEachLarge_DoNotShowTheirOnePersonIntersection()
    {
        // Arrange: R7-M2, collector A has six completed responses and version 2 has six, but only Bob answered v2 through A.
        var cancellationToken = TestContext.Current.CancellationToken;
        var v1 = Version("v2", "survey2");
        var v2 = ResultsSqliteStore.Version("v2b", "survey2", 2, ResultsSqliteStore.ChoiceQuestion("q1", "color", "red", "blue"));

        await using (var write = _store.OpenSession())
        {
            await write.SaveAsync(v2, false, SurveysConstants.CollectionName, cancellationToken);

            foreach (var (id, version, collector) in new[] { ("a1", v1, "A"), ("a2", v1, "A"), ("a3", v1, "A"), ("a4", v1, "A"), ("a5", v1, "A"), ("bob", v2, "A"), ("b1", v2, "B"), ("b2", v2, "B"), ("b3", v2, "B"), ("b4", v2, "B"), ("b5", v2, "B") })
            {
                var response = ResultsSqliteStore.Response(id, version, SurveyResponseStatus.Completed, _day, ("q1", new SurveyAnswer { Values = ["red"] }));
                response.CollectorId = collector;
                await write.SaveAsync(response, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
            }

            await write.SaveChangesAsync(cancellationToken);
        }

        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var rows = await ReadAsync(source, "SurveyResponses", TestSecurity.User("kim"));

        // Assert: Bob's row, the one version-2 row of collector A, shows nowhere; the large groups show their version.
        Assert.DoesNotContain("bob", rows.Keys);
        Assert.Equal(1L, rows["a1"]["VersionNumber"]);
        Assert.Equal(2L, rows["b1"]["VersionNumber"]);
    }

    [Fact]
    public async Task SurveyResponses_AnAnonymousSurveyLargerThanOnePage_IsReadPageByPageInIdOrder()
    {
        // Arrange: R7-Mi16, more anonymous responses than one page of the read apart (500).
        await AddPlainAnonymousAsync("k", 505, _day);
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var ids = (await ReadIdsAsync(source, "SurveyResponses", 1000)).Where(id => id != "r1").ToList();

        // Assert: every row once, in id order, across the page boundary (the next page starts after the last id read).
        Assert.Equal(506, ids.Count);
        Assert.Equal(ids.Order(StringComparer.Ordinal), ids);
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task SurveyResponses_AConditionOnAFieldFilledInAfterTheRead_CountsTheRowsTheConsumerKeeps()
    {
        // Arrange: R7-Mi21, five anonymous responses on the collector named "main" (the name is looked up after the read).
        var cancellationToken = TestContext.Current.CancellationToken;
        await AddPlainAnonymousAsync("p", 4, _day);

        await using (var write = _store.OpenSession())
        {
            await write.SaveAsync(new SurveyCollector { ItemId = "collector1", SurveyId = "survey2", Name = "main" }, false, SurveysConstants.CollectionName, cancellationToken);
            await write.SaveChangesAsync(cancellationToken);
        }

        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var ids = await ReadIdsAsync(source, "SurveyResponses", new DataCondition { Field = "Collector", Operator = DataFilterOperator.Equals, Values = ["main"] });

        // Assert: the collector's name is filled in before the group is counted, so the five count and show (counted as
        // empty, they would hide every row).
        Assert.Equal(["p0", "p1", "p2", "p3", "r2"], ids.Where(id => id != "r1").Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SurveyResponses_AJointKeyThatReachesTheMinimumLater_IsReleasedPastAnIncrementalReadsWatermark(bool readsDocuments)
    {
        // Arrange: R8-M6, the usual collector reaches the minimum of 5 by 10 November (r2 and four more), and a new
        // collector has four responses on 9 November: too few to show, and too few to hide in "Other".
        await AddPlainAnonymousAsync("p", 4, _day.AddDays(-2));
        await AddAnonymousAsync("b", 4, _day.AddDays(-1), SurveyResponseStatus.Completed, "late");
        var extra = DocumentsCondition(readsDocuments);
        DateTime watermark;

        await using (var session = _store.OpenSession())
        {
            var first = await ReadAsync(Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses), "SurveyResponses", TestSecurity.User("kim"), conditions: extra);
            Assert.DoesNotContain(first.Keys, id => id.StartsWith('b'));
            watermark = first.Values.Max(row => Assert.IsType<DateTime>(row["ModifiedUtc"]));
        }

        // The new collector's fifth response comes in on 11 November.
        await AddAnonymousAsync("b", 1, _day.AddDays(1), SurveyResponseStatus.Completed, "late", start: 4);

        // Act: the next run keeps the rows changed after the largest change time the first run read.
        await using var next = _store.OpenSession();
        var rows = (await ReadAsync(Source(next, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses), "SurveyResponses", TestSecurity.User("kim"), conditions: [.. extra, After(watermark)]))
            .Where(row => Assert.IsType<DateTime>(row.Value["ModifiedUtc"]) > watermark)
            .ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);

        // Assert: the collector's four earlier rows are released when its fifth response's day ends, and read that moment,
        // so the incremental read gets them; the rows the first run read aren't read again.
        Assert.Equal(["b0", "b1", "b2", "b3", "b4"], rows.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(_day.Date.AddDays(2), Assert.IsType<DateTime>(rows["b0"]["ModifiedUtc"]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SurveyResponses_AStatusThatReachesTheMinimumLater_IsReleasedPastAnIncrementalReadsWatermark(bool readsDocuments)
    {
        // Arrange: R8-M6, six completed responses, and four abandoned ones on 9 November (too few to show).
        await AddPlainAnonymousAsync("p", 5, _day.AddDays(-2));
        await AddAnonymousAsync("x", 4, _day.AddDays(-1), SurveyResponseStatus.Abandoned);
        var extra = DocumentsCondition(readsDocuments);
        DateTime watermark;

        await using (var session = _store.OpenSession())
        {
            var first = await ReadAsync(Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses), "SurveyResponses", TestSecurity.User("kim"), conditions: extra);
            Assert.DoesNotContain(first.Keys, id => id.StartsWith('x'));
            watermark = first.Values.Max(row => Assert.IsType<DateTime>(row["ModifiedUtc"]));
        }

        // The fifth abandoned response comes in on 11 November.
        await AddAnonymousAsync("x", 1, _day.AddDays(1), SurveyResponseStatus.Abandoned, start: 4);

        // Act
        await using var next = _store.OpenSession();
        var rows = (await ReadAsync(Source(next, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses), "SurveyResponses", TestSecurity.User("kim"), conditions: [.. extra, After(watermark)]))
            .Where(row => Assert.IsType<DateTime>(row.Value["ModifiedUtc"]) > watermark)
            .ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);

        // Assert: the status shows once its fifth response's day ends, and its earlier rows read that moment.
        Assert.Equal(["x0", "x1", "x2", "x3", "x4"], rows.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(_day.Date.AddDays(2), Assert.IsType<DateTime>(rows["x0"]["ModifiedUtc"]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SurveyResponses_TodaysResponses_DoNotCountTowardAStatusReachingTheMinimum(bool readsDocuments)
    {
        // Arrange: R8-M6, a released survey with one abandoned response from 9 November and four abandoned today.
        await AddPlainAnonymousAsync("p", 5, _day.AddDays(-2));
        await AddAnonymousAsync("old", 1, _day.AddDays(-1), SurveyResponseStatus.Abandoned);
        await AddAnonymousAsync("now", 4, DateTime.UtcNow, SurveyResponseStatus.Abandoned);
        await using var session = _store.OpenSession();
        var source = Source(session, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);
        var abandoned = new DataCondition { Field = "Status", Operator = DataFilterOperator.In, Values = [nameof(SurveyResponseStatus.Abandoned)] };

        // Act
        var ids = await ReadIdsAsync(source, "SurveyResponses", [.. DocumentsCondition(readsDocuments), abandoned]);

        // Assert: only one abandoned response has ended its day, so the status doesn't show yet (counted with today's,
        // the old response would show as a group of one).
        Assert.DoesNotContain("old0", ids);
        Assert.DoesNotContain(ids, id => id.StartsWith("now", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SurveyResponses_APreviewOfAnotherSurvey_DoesNotReadTheAnonymousSurveysResponses()
    {
        // Arrange: R8-M7, a released anonymous survey besides the identified one.
        await AddPlainAnonymousAsync("p", 20, _day);
        await using var session = _store.OpenSession();
        var recorder = new RecordingFieldProvider();
        var source = Source(session, new ServiceCollection().BuildServiceProvider(), [recorder], SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act: a ten-row preview of the identified survey.
        var ids = await ReadIdsAsync(source, "SurveyResponses", 10, new DataCondition { Field = "SurveyId", Operator = DataFilterOperator.Equals, Values = ["survey1"] });

        // Assert: the condition rules the anonymous survey out before its count, so none of its responses is read.
        Assert.Equal(["r1"], ids);
        Assert.DoesNotContain("survey2", recorder.Surveys);
    }

    [Fact]
    public async Task SurveyResponses_AReadWithoutConditions_CountsTheAnonymousSurveyInSql_AndReadsEachResponseOnce()
    {
        // Arrange: R8-M7, a released anonymous survey of 21 responses.
        await AddPlainAnonymousAsync("p", 20, _day);
        await using var session = _store.OpenSession();
        var recorder = new RecordingFieldProvider();
        var source = Source(session, new ServiceCollection().BuildServiceProvider(), [recorder], SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var ids = await ReadIdsAsync(source, "SurveyResponses", 100);

        // Assert: every row shows, and each response is read once, for its row (the count reads the index only).
        Assert.Equal(22, ids.Count);
        Assert.Equal(21, recorder.Surveys.Count(id => id == "survey2"));
    }

    [Fact]
    public async Task SurveyResponses_AOneDayFilterOnModifiedAfterADaylightSavingChange_FindsTheFirstHourOfTheLongDay()
    {
        // Arrange: R8-Mi23, the site is in New York, where 1 November 2026 lasts 25 hours (the clocks go back at 02:00).
        // The survey was released in October; one response came in at 00:30 on 1 November (04:30 UTC, still daylight time).
        await AddPlainAnonymousAsync("p", 5, new DateTime(2026, 10, 28, 16, 0, 0, DateTimeKind.Utc));
        await AddPlainAnonymousAsync("early", 1, new DateTime(2026, 11, 1, 4, 30, 0, DateTimeKind.Utc));
        await using var session = _store.OpenSession();
        var source = Source(session, Zones("America/New_York", "America/New_York"), SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);
        var shownDay = new DateTime(2026, 11, 2, 5, 0, 0, DateTimeKind.Utc);

        // Act: "Modified on 2 November", the day the rows of 1 November show as changed on (the end of their day).
        var ids = await ReadIdsAsync(
            source,
            "SurveyResponses",
            new DataCondition { Field = "ModifiedUtc", Operator = DataFilterOperator.GreaterThanOrEqual, Values = [shownDay] },
            new DataCondition { Field = "ModifiedUtc", Operator = DataFilterOperator.LessThanOrEqual, Values = [shownDay.AddDays(1).AddSeconds(-1)] });

        // Assert: the raw read starts at the start of 1 November in New York (04:00 UTC), not 24 hours before 2 November
        // (05:00 UTC), so the response of its first hour is read.
        Assert.Contains("early0", ids);
    }

    [Fact]
    public async Task SurveyResponses_AnonymousLabels_StayWithTheirRowsAcrossPages()
    {
        // Arrange: R8-Mi34, more anonymous responses than one page of the read apart (500), on a survey with its secret,
        // so each response has a keyed label.
        var cancellationToken = TestContext.Current.CancellationToken;
        await AddPlainAnonymousAsync("k", 505, _day);

        await using (var write = _store.OpenSession())
        {
            var existing = await new SurveyStore(write).FindByIdAsync("survey2", cancellationToken);
            existing.AnonymitySecret = "plain-test-secret";
            await write.SaveAsync(existing, false, SurveysConstants.CollectionName, cancellationToken);
            await write.SaveChangesAsync(cancellationToken);
        }

        await using var session = _store.OpenSession();
        var store = new SurveyStore(session);
        var services = new ServiceCollection()
            .AddSingleton(store)
            .AddSingleton(new SurveyAnonymitySecrets(new EphemeralDataProtectionProvider(), store, NullLogger<SurveyAnonymitySecrets>.Instance))
            .AddSingleton(provider => new SurveyRespondentNames(provider, RecipeRoundTrip.Localizer<SurveyRespondentNames>()))
            .BuildServiceProvider();
        var source = Source(session, services, SurveysPermissions.ViewSurveyResponses, SurveysPermissions.ViewOwnSurveyResponses);

        // Act
        var rows = await ReadAsync(source, "SurveyResponses", TestSecurity.User("kim"), maxRows: 1000);
        var labels = rows.Where(row => row.Key != "r1").Select(row => (string)row.Value["Number"]).ToList();

        // Assert: every anonymous row carries its own keyed label.
        Assert.Equal(506, labels.Count);
        Assert.All(labels, label => Assert.False(string.IsNullOrEmpty(label)));
        Assert.Equal(labels.Count, labels.Distinct(StringComparer.Ordinal).Count());
    }

    // A condition every response passes that the data set evaluates on its documents, so the anonymous survey's count
    // reads them instead of the index.
    private static DataCondition[] DocumentsCondition(bool readsDocuments)
        => readsDocuments ? [new DataCondition { Field = "Grading", Operator = DataFilterOperator.IsNotEmpty }] : [];

    // A pipeline's watermark condition: rows changed after the last run.
    private static DataCondition After(DateTime watermark)
        => new() { Field = "ModifiedUtc", Operator = DataFilterOperator.GreaterThan, Values = [watermark] };

    // Adds completed responses to the anonymous survey on its usual collector and with no context, the group r2 is in.
    private Task AddPlainAnonymousAsync(string prefix, int count, DateTime changedUtc)
        => AddAnonymousAsync(prefix, count, changedUtc, SurveyResponseStatus.Completed);

    // Adds responses to the anonymous survey, with no context.
    private async Task AddAnonymousAsync(string prefix, int count, DateTime changedUtc, SurveyResponseStatus status, string collectorId = "collector1", int start = 0)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var session = _store.OpenSession();

        for (var index = start; index < start + count; index++)
        {
            var response = ResultsSqliteStore.Response($"{prefix}{index}", Version("v2", "survey2"), status, changedUtc, ("q1", new SurveyAnswer { Values = ["red"] }));
            response.CollectorId = collectorId;
            await session.SaveAsync(response, false, SurveysConstants.ResponsesCollectionName, cancellationToken);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    // Records the survey of every response whose row the data set completes, which happens once per document read.
    private sealed class RecordingFieldProvider : ISurveyResponseFieldProvider
    {
        public List<string> Surveys { get; } = [];

        public IEnumerable<(string Name, string DisplayName, DataFieldType Type)> DescribeFields()
            => [];

        public Task WriteFieldsAsync(SurveyResponse response, IDictionary<string, object> row, CancellationToken cancellationToken = default)
        {
            Surveys.Add(response.SurveyId);

            return Task.CompletedTask;
        }
    }

    // The services of a site in one time zone whose viewer picked another for themselves.
    private static ServiceProvider Zones(string siteZone, string viewerZone)
    {
        var siteService = new Mock<global::OrchardCore.Settings.ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(new global::OrchardCore.Settings.SiteSettings { TimeZoneId = siteZone });
        var zone = new Mock<global::OrchardCore.Modules.ITimeZone>();
        zone.SetupGet(value => value.TimeZoneId).Returns(viewerZone);
        var localClock = new Mock<global::OrchardCore.Modules.ILocalClock>();
        localClock.Setup(clock => clock.GetLocalTimeZoneAsync()).ReturnsAsync(zone.Object);

        return new ServiceCollection().AddSingleton(siteService.Object).AddSingleton(localClock.Object).BuildServiceProvider();
    }

    private static SurveysDataSource Source(ISession session, params Permission[] grants)
        => Source(session, new ServiceCollection().BuildServiceProvider(), grants);

    private static SurveysDataSource Source(ISession session, IServiceProvider services, params Permission[] grants)
        => Source(session, services, [], grants);

    private static SurveysDataSource Source(ISession session, IServiceProvider services, ISurveyResponseFieldProvider[] providers, params Permission[] grants)
    {
        var authorization = TestSecurity.Granting(grants);
        var reader = new SurveyIndexReader(session, new SurveyStore(session));
        var access = new SurveyDataSetAccess(new SurveyAccessScopes(authorization, [], reader), reader, services, new RuntimeTestClock { UtcNow = DateTime.UtcNow });
        var registry = Registry();
        var versions = new SurveyVersionStore(session);
        var names = new SurveyRespondentNames(services, RecipeRoundTrip.Localizer<SurveyRespondentNames>());

        ISurveysDataSet[] sets =
        [
            new SurveyResponsesDataSet(session, authorization, access, providers, names, versions, new SurveyCollectorStore(session), RecipeRoundTrip.Localizer<SurveyResponsesDataSet>()),
            new SurveyAnswersDataSet(session, authorization, access, new ResultsVersionCache(versions), registry, RecipeRoundTrip.Localizer<SurveyAnswersDataSet>()),
            new SurveyPageVisitsDataSet(session, authorization, access, new ResultsVersionCache(versions), RecipeRoundTrip.Localizer<SurveyPageVisitsDataSet>()),
        ];

        return new SurveysDataSource(sets, new SurveyWideDataSetFactory(session, authorization, access, new SurveyStore(session), versions, registry, names, RecipeRoundTrip.Localizer<SurveyWideDataSet>()));
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>>> ReadAsync(SurveysDataSource source, string dataSet, ClaimsPrincipal user, string key = "Id", DataCondition[] conditions = null, int maxRows = 100)
    {
        var query = new DataSourceQuery
        {
            DataSet = dataSet,
            MaxRows = maxRows,
            Context = new DataSourceContext { User = user },
        };

        foreach (var condition in conditions ?? [])
        {
            query.Conditions.Add(condition);
        }

        var table = await ReportDesignerTestServices.ReadAsync(source, query, TestContext.Current.CancellationToken);

        var fields = table.Fields.Select(field => field.Name).ToList();
        var keyIndex = fields.IndexOf(key);

        return table.Rows.ToDictionary(
            row => (string)row[keyIndex],
            row => (IReadOnlyDictionary<string, object>)fields.Select((name, index) => (name, value: row[index])).ToDictionary(pair => pair.name, pair => pair.value, StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    private static Task<List<string>> ReadIdsAsync(SurveysDataSource source, string dataSet, params DataCondition[] conditions)
        => ReadIdsAsync(source, dataSet, 100, conditions);

    private static async Task<List<string>> ReadIdsAsync(SurveysDataSource source, string dataSet, int maxRows, params DataCondition[] conditions)
    {
        var query = new DataSourceQuery
        {
            DataSet = dataSet,
            MaxRows = maxRows,
            Context = new DataSourceContext { User = TestSecurity.User("kim") },
        };

        foreach (var condition in conditions)
        {
            query.Conditions.Add(condition);
        }

        var table = await ReportDesignerTestServices.ReadAsync(source, query, TestContext.Current.CancellationToken);
        var index = table.Fields.Select(field => field.Name).ToList().IndexOf("Id");

        return [.. table.Rows.Select(row => (string)row[index])];
    }

    private static SurveyControlRegistry Registry()
    {
        var options = new SurveyControlOptions();
        options.Controls[ResultsTestControlHandler.Name] = new SurveyControlDescriptor
        {
            Name = ResultsTestControlHandler.Name,
            Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.UsesChoices,
        };

        return new SurveyControlRegistry(Options.Create(options), [new ResultsTestControlHandler()], [], []);
    }

    private static Survey Survey(string id, string name, string versionId, string ownerId, AnonymityMode anonymity)
        => new()
        {
            ItemId = id,
            Name = name,
            Title = name.ToUpperInvariant(),
            Status = SurveyStatus.Open,
            PublishedVersionId = versionId,
            PublishedVersionNumber = 1,
            OwnerId = ownerId,
            Access = new SurveyAccessSettings { Anonymity = anonymity },
        };

    private static SurveyVersion Version(string id, string surveyId)
        => ResultsSqliteStore.Version(id, surveyId, 1, ResultsSqliteStore.ChoiceQuestion("q1", "color", "red", "blue"));
}
