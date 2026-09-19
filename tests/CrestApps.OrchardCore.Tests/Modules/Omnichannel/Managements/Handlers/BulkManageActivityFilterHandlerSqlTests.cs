using CrestApps.Core.Omnichannel.Services;
using CrestApps.Core.Omnichannel.Models;
using System.Data.Common;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Handlers;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Data.Sqlite;
using OrchardCore;
using OrchardCore.Data;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.PostgreSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Handlers;

/// <summary>
/// A bulk action on the manage screen — complete these, purge these, reassign these — does whatever the statement
/// built here matches, and nothing downstream looks at the population again before it acts. Three ways that goes
/// wrong are covered here. A do-not-call join that is emitted when nobody asked for one narrows the action to the
/// people who have opted out, so an operator who meant "close out this campaign's backlog" closes out only the
/// contacts who asked to be left alone, and the rest stay live. An attempt bound that is off by one dials a batch
/// of people again who had already been dialled enough. And a filter value pasted into the statement instead of
/// bound to it hands the query string control of the database.
/// </summary>
public sealed class BulkManageActivityFilterHandlerSqlTests
{
    private const string ActivityAlias = "a";
    private const string DoNotCallAlias = "dnc";

    private static readonly DateTime _now = new(2026, 7, 16, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task FilteringAsync_WhenNoContactFilterIsChosen_DoesNotJoinTheDoNotCallIndex()
    {
        // Arrange
        // This is the one that quietly acts on the wrong people. The do-not-call join is an INNER JOIN onto the
        // communication-preference index with the flag pinned on, so if it is ever added for a filter that did not
        // ask for it, every activity whose contact has not opted out disappears from the match. The operator sees
        // a plausible-looking smaller number, confirms, and the action lands on the opted-out population only.
        var filter = new BulkManageActivityFilter
        {
            Status = ActivityStatus.NotStated,
            Channel = OmnichannelConstants.Channels.Phone,
        };
        var context = CreateContext(filter, new SqliteDialect());
        var handler = new BulkManageActivityFilterHandler();

        // Act
        await handler.FilteringAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var sql = context.SqlBuilder.ToSqlString();

        Assert.DoesNotContain("JOIN", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(OmnichannelContactCommunicationPreferenceIndex), sql, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(OmnichannelContactCommunicationPreferenceIndex.DoNotCall), sql, StringComparison.Ordinal);
        Assert.DoesNotContain(context.Parameters, parameter => parameter.Key.StartsWith("@Dnc", StringComparison.Ordinal));

        // The filters that were asked for are still there, so the assertions above cannot pass on an empty statement.
        Assert.Equal((int)ActivityStatus.NotStated, Assert.IsType<int>(context.Parameters["@Status"]));
        Assert.Equal(OmnichannelConstants.Channels.Phone, Assert.IsType<string>(context.Parameters["@Channel"]));
    }

    [Fact]
    public async Task FilteringAsync_WhenOnlyPhoneAndTimeZoneFiltersAreChosen_StillDoesNotJoinTheDoNotCallIndex()
    {
        // Arrange
        // The phone and time-zone filters share a code path with the do-not-call one: all three are contact-level
        // and all three are applied together once any of them is present. "Some join exists" is therefore not
        // enough to tell whether the do-not-call join leaked in, which is what makes this the realistic near miss.
        var filter = new BulkManageActivityFilter
        {
            PhoneNumber = "7024991234",
            TimeZoneIds = ["America/Los_Angeles"],
        };
        var context = CreateContext(filter, new SqliteDialect());
        var handler = new BulkManageActivityFilterHandler();

        // Act
        await handler.FilteringAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var sql = context.SqlBuilder.ToSqlString();

        Assert.Contains(nameof(OmnichannelContactIndex), sql, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(OmnichannelContactCommunicationPreferenceIndex), sql, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(OmnichannelContactCommunicationPreferenceIndex.DoNotCall), sql, StringComparison.Ordinal);
        Assert.DoesNotContain(context.Parameters, parameter => parameter.Key.StartsWith("@Dnc", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FilteringAsync_WhenADoNotCallRangeIsChosen_ComparesTheFlagWithABoundValueRatherThanAnIntegerLiteral()
    {
        // Arrange
        // DoNotCall is declared as a bool, which YesSql creates as "boolean" on PostgreSQL and as "BOOL" — numeric
        // affinity, holding 0 and 1 — on SQLite. Comparing it to the integer literal 1 is accepted by SQLite and
        // rejected outright by PostgreSQL with "operator does not exist: boolean = integer", so the whole bulk
        // manage screen works in development and throws on a PostgreSQL deployment the moment anybody picks a
        // do-not-call date range. Binding the value instead lets each provider render its own boolean literal.
        var dialect = new PostgreSqlDialect();
        var filter = new BulkManageActivityFilter
        {
            DoNotCallFrom = _now.AddDays(-30),
        };
        var context = CreateContext(filter, dialect);
        var handler = new BulkManageActivityFilterHandler();

        // Act
        await handler.FilteringAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var sql = context.SqlBuilder.ToSqlString();
        var doNotCallColumn = $"{dialect.QuoteForAliasName(DoNotCallAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelContactCommunicationPreferenceIndex.DoNotCall))}";
        var columnIndex = sql.IndexOf(doNotCallColumn, StringComparison.Ordinal);

        Assert.True(columnIndex >= 0, $"The do-not-call filter must constrain {doNotCallColumn}. Statement: {sql}");
        Assert.StartsWith(" = @", sql.Substring(columnIndex + doNotCallColumn.Length), StringComparison.Ordinal);
        Assert.Contains(context.Parameters, parameter => parameter.Value is bool isFlagged && isFlagged);
    }

    [Fact]
    public async Task FilteringAsync_WhenFilterValuesContainSqlSyntax_BindsThemAsParametersInsteadOfInliningThem()
    {
        // Arrange
        // Every one of these values reaches the handler straight from the query string of the manage screen, and
        // the handler is the only thing between them and a statement that is executed as raw SQL rather than
        // through the YesSql query API. This is the injection boundary, so it is asserted rather than assumed.
        const string Injection = "'; DROP TABLE \"Document\"; --";
        var filter = new BulkManageActivityFilter
        {
            SubjectContentType = $"LeadFollowUp{Injection}",
            Channel = $"Phone{Injection}",
            Source = $"Campaign{Injection}",
            CampaignId = $"campaign{Injection}",
            AssignedToUserIds = [$"agent{Injection}", $"supervisor{Injection}"],
            TimeZoneIds = [$"America/Los_Angeles{Injection}"],
        };
        var context = CreateContext(filter, new SqliteDialect());
        var handler = new BulkManageActivityFilterHandler();

        // Act
        await handler.FilteringAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var sql = context.SqlBuilder.ToSqlString();
        string[] suppliedValues =
        [
            filter.SubjectContentType,
            filter.Channel,
            filter.Source,
            filter.CampaignId,
            filter.AssignedToUserIds[0],
            filter.AssignedToUserIds[1],
            filter.TimeZoneIds[0],
        ];

        Assert.DoesNotContain(Injection, sql, StringComparison.Ordinal);

        foreach (var suppliedValue in suppliedValues)
        {
            Assert.DoesNotContain(suppliedValue, sql, StringComparison.Ordinal);
            Assert.Contains(context.Parameters, parameter => string.Equals(parameter.Value as string, suppliedValue, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task FilteringAsync_WhenDateRangesAreChosen_BindsEveryBoundAsAParameter()
    {
        // Arrange
        // Dates are the values most likely to be formatted into a statement by hand, because every provider spells
        // a date literal differently and a parameter sidesteps the question. Asserting no year appears anywhere in
        // the text is the cheap way to catch a bound that got written out instead of bound.
        var filter = new BulkManageActivityFilter
        {
            ScheduledFrom = _now.AddDays(-7),
            ScheduledTo = _now.AddDays(7),
            CreatedFrom = _now.AddDays(-30),
            CreatedTo = _now,
            DoNotCallFrom = _now.AddDays(-14),
            DoNotCallTo = _now.AddDays(-1),
        };
        var context = CreateContext(filter, new SqliteDialect());
        var handler = new BulkManageActivityFilterHandler();

        // Act
        await handler.FilteringAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var sql = context.SqlBuilder.ToSqlString();

        Assert.DoesNotContain(_now.Year.ToString(), sql, StringComparison.Ordinal);
        Assert.Equal(filter.ScheduledFrom.Value, Assert.IsType<DateTime>(context.Parameters["@ScheduledFrom"]));
        Assert.Equal(filter.ScheduledTo.Value, Assert.IsType<DateTime>(context.Parameters["@ScheduledTo"]));
        Assert.Equal(filter.CreatedFrom.Value, Assert.IsType<DateTime>(context.Parameters["@CreatedFrom"]));
        Assert.Equal(filter.CreatedTo.Value, Assert.IsType<DateTime>(context.Parameters["@CreatedTo"]));
        Assert.Equal(filter.DoNotCallFrom.Value, Assert.IsType<DateTime>(context.Parameters["@DncFrom"]));
        Assert.Equal(filter.DoNotCallTo.Value, Assert.IsType<DateTime>(context.Parameters["@DncTo"]));
    }

    [Theory]
    [InlineData("2+", 2)]
    [InlineData("5+", 5)]
    [InlineData("10+", 10)]
    public async Task FilteringAsync_WhenTheAttemptFilterEndsWithAPlus_EmitsAMinimumBound(string attemptFilter, int expectedMinimum)
    {
        // Arrange
        // The attempt filter is a tiny hand-written grammar carried in a query string — a trailing plus, a trailing
        // minus, or a bare number — and each form points the comparison a different way. Confusing a floor for a
        // ceiling inverts the population a bulk action covers: "stop chasing the ones we have already chased hard"
        // becomes "act on everybody except them".
        var filter = new BulkManageActivityFilter
        {
            AttemptFilter = attemptFilter,
        };
        var context = CreateContext(filter, new SqliteDialect());
        var handler = new BulkManageActivityFilterHandler();

        // Act
        await handler.FilteringAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var sql = context.SqlBuilder.ToSqlString();

        Assert.Contains($"{AttemptsColumn(context.Dialect)} >= @MinAttempts", sql, StringComparison.Ordinal);
        Assert.Equal(expectedMinimum, Assert.IsType<int>(context.Parameters["@MinAttempts"]));
        Assert.DoesNotContain("@MaxAttempts", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("@ExactAttempts", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2-", 2)]
    [InlineData("5-", 5)]
    [InlineData("10-", 10)]
    public async Task FilteringAsync_WhenTheAttemptFilterEndsWithAMinus_EmitsAMaximumBound(string attemptFilter, int expectedMaximum)
    {
        // Arrange
        var filter = new BulkManageActivityFilter
        {
            AttemptFilter = attemptFilter,
        };
        var context = CreateContext(filter, new SqliteDialect());
        var handler = new BulkManageActivityFilterHandler();

        // Act
        await handler.FilteringAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var sql = context.SqlBuilder.ToSqlString();

        Assert.Contains($"{AttemptsColumn(context.Dialect)} <= @MaxAttempts", sql, StringComparison.Ordinal);
        Assert.Equal(expectedMaximum, Assert.IsType<int>(context.Parameters["@MaxAttempts"]));
        Assert.DoesNotContain("@MinAttempts", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("@ExactAttempts", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2", 2)]
    [InlineData("4", 4)]
    [InlineData("9", 9)]
    public async Task FilteringAsync_WhenTheAttemptFilterIsABareNumber_EmitsAnExactMatch(string attemptFilter, int expectedAttempts)
    {
        // Arrange
        // A bare number is the "exactly this many attempts" bucket on the screen, not a floor and not a ceiling.
        // Reading it as either would pull in every neighbouring bucket and hand a bulk action a far larger set
        // than the number the operator was shown when they confirmed it.
        var filter = new BulkManageActivityFilter
        {
            AttemptFilter = attemptFilter,
        };
        var context = CreateContext(filter, new SqliteDialect());
        var handler = new BulkManageActivityFilterHandler();

        // Act
        await handler.FilteringAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var sql = context.SqlBuilder.ToSqlString();

        Assert.Contains($"{AttemptsColumn(context.Dialect)} = @ExactAttempts", sql, StringComparison.Ordinal);
        Assert.Equal(expectedAttempts, Assert.IsType<int>(context.Parameters["@ExactAttempts"]));
        Assert.DoesNotContain("@MinAttempts", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("@MaxAttempts", sql, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FilteringAsync_WhenTheAttemptFilterIsOnePlus_ClampsTheMinimumToTwoAttempts()
    {
        // Arrange
        // The lowest "or more" bucket the screen offers is "2+", so "1+" only arrives from a hand-edited query
        // string. Taken literally it would match every activity that has ever been dialled, which is close to the
        // whole inventory — the opposite of a narrowing filter. The clamp makes it mean "has been retried".
        var filter = new BulkManageActivityFilter
        {
            AttemptFilter = "1+",
        };
        var context = CreateContext(filter, new SqliteDialect());
        var handler = new BulkManageActivityFilterHandler();

        // Act
        await handler.FilteringAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var sql = context.SqlBuilder.ToSqlString();

        Assert.Contains($"{AttemptsColumn(context.Dialect)} >= @MinAttempts", sql, StringComparison.Ordinal);
        Assert.Equal(2, Assert.IsType<int>(context.Parameters["@MinAttempts"]));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    public async Task FilteringAsync_WhenTheAttemptFilterIsZeroOrOne_ClampsTheMaximumToOneAttempt(string attemptFilter)
    {
        // Arrange
        // "No attempt" is the screen's option for work that has not been worked yet, and an activity can carry its
        // first attempt before anybody has actually reached the contact. Both 0 and 1 therefore mean "not retried",
        // and the bound is a constant rather than a bound value, so no exact-match parameter may be left behind.
        var filter = new BulkManageActivityFilter
        {
            AttemptFilter = attemptFilter,
        };
        var context = CreateContext(filter, new SqliteDialect());
        var handler = new BulkManageActivityFilterHandler();

        // Act
        await handler.FilteringAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var sql = context.SqlBuilder.ToSqlString();

        Assert.Contains($"{AttemptsColumn(context.Dialect)} <= 1", sql, StringComparison.Ordinal);
        Assert.DoesNotContain(context.Parameters, parameter => parameter.Key.EndsWith("Attempts", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("any")]
    [InlineData("abc+")]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData("2 or more")]
    public async Task FilteringAsync_WhenTheAttemptFilterIsNotANumber_EmitsNoAttemptPredicate(string attemptFilter)
    {
        // Arrange
        // An expression nobody can parse must widen the result rather than narrow it to something arbitrary. The
        // alternative — falling back to a default bound — would let a typo in a bookmarked URL silently change
        // which activities a bulk action covers, with no visible sign on the screen that it did.
        var filter = new BulkManageActivityFilter
        {
            AttemptFilter = attemptFilter,
        };
        var context = CreateContext(filter, new SqliteDialect());
        var handler = new BulkManageActivityFilterHandler();

        // Act
        await handler.FilteringAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var sql = context.SqlBuilder.ToSqlString();

        Assert.DoesNotContain(nameof(OmnichannelActivityIndex.Attempts), sql, StringComparison.Ordinal);
        Assert.DoesNotContain(context.Parameters, parameter => parameter.Key.EndsWith("Attempts", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PageBulkManageableAsync_WhenADoNotCallRangeIsChosen_ReturnsOnlyContactsStillFlaggedDoNotCall()
    {
        // Arrange
        // The opt-out date outlives the opt-out: somebody who asked to be left alone and later asked to be called
        // again keeps their DoNotCallUtc and clears the flag. Matching on the date alone would put them back on a
        // do-not-call working list, and matching on neither would sweep in contacts who never opted out at all.
        var databasePath = DatabasePath("do-not-call-flag");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            var optedOutContactId = IdGenerator.GenerateId();
            var optedBackInContactId = IdGenerator.GenerateId();
            var neverAskedContactId = IdGenerator.GenerateId();
            string optedOutItemId;

            await using (var seedSession = store.CreateSession())
            {
                optedOutItemId = await SaveActivityAsync(seedSession, optedOutContactId);
                await SaveActivityAsync(seedSession, optedBackInContactId);
                await SaveActivityAsync(seedSession, neverAskedContactId);
                await SavePreferenceAsync(seedSession, optedOutContactId, doNotCall: true, _now.AddDays(-5));
                await SavePreferenceAsync(seedSession, optedBackInContactId, doNotCall: false, _now.AddDays(-5));
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var querySession = store.CreateSession();
            var activityStore = CreateActivityStore(querySession, store, connectionString);

            // Act
            var result = await activityStore.PageBulkManageableAsync(
                1,
                10,
                new BulkManageActivityFilter
                {
                    DoNotCallFrom = _now.AddDays(-30),
                },
                TestContext.Current.CancellationToken);

            // Assert
            var activity = Assert.Single(result.Entries);
            Assert.Equal(optedOutItemId, activity.ItemId);
            Assert.Equal(1, result.Count);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task PageBulkManageableAsync_WhenOnlyADoNotCallStartIsChosen_IncludesOptOutsOnTheStartAndExcludesEarlierOnes()
    {
        // Arrange
        // The screen presents this as a date range, and a date range that a person picks is read inclusively: a
        // contact who opted out on the first day of the window belongs in it. Excluding the boundary leaves that
        // day's opt-outs out of an audit or clean-up run, and nothing on the screen would show they were missed.
        var databasePath = DatabasePath("do-not-call-from");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            var start = _now.AddDays(-1);
            string earlierItemId;
            string boundaryItemId;
            string laterItemId;

            await using (var seedSession = store.CreateSession())
            {
                earlierItemId = await SaveOptedOutContactActivityAsync(seedSession, _now.AddDays(-2));
                boundaryItemId = await SaveOptedOutContactActivityAsync(seedSession, start);
                laterItemId = await SaveOptedOutContactActivityAsync(seedSession, _now);
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var querySession = store.CreateSession();
            var activityStore = CreateActivityStore(querySession, store, connectionString);

            // Act
            var result = await activityStore.PageBulkManageableAsync(
                1,
                10,
                new BulkManageActivityFilter
                {
                    DoNotCallFrom = start,
                },
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(
                new[] { boundaryItemId, laterItemId }.OrderBy(itemId => itemId, StringComparer.Ordinal),
                result.Entries.Select(activity => activity.ItemId).OrderBy(itemId => itemId, StringComparer.Ordinal));
            Assert.DoesNotContain(earlierItemId, result.Entries.Select(activity => activity.ItemId));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task PageBulkManageableAsync_WhenOnlyADoNotCallEndIsChosen_IncludesOptOutsOnTheEndAndExcludesLaterOnes()
    {
        // Arrange
        // The upper bound has to behave the same way as the lower one, and it is the one that decides whether the
        // most recent opt-outs are in scope. An exclusive end silently drops the final day of every window.
        var databasePath = DatabasePath("do-not-call-to");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            var end = _now.AddDays(-1);
            string earlierItemId;
            string boundaryItemId;
            string laterItemId;

            await using (var seedSession = store.CreateSession())
            {
                earlierItemId = await SaveOptedOutContactActivityAsync(seedSession, _now.AddDays(-2));
                boundaryItemId = await SaveOptedOutContactActivityAsync(seedSession, end);
                laterItemId = await SaveOptedOutContactActivityAsync(seedSession, _now);
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var querySession = store.CreateSession();
            var activityStore = CreateActivityStore(querySession, store, connectionString);

            // Act
            var result = await activityStore.PageBulkManageableAsync(
                1,
                10,
                new BulkManageActivityFilter
                {
                    DoNotCallTo = end,
                },
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(
                new[] { earlierItemId, boundaryItemId }.OrderBy(itemId => itemId, StringComparer.Ordinal),
                result.Entries.Select(activity => activity.ItemId).OrderBy(itemId => itemId, StringComparer.Ordinal));
            Assert.DoesNotContain(laterItemId, result.Entries.Select(activity => activity.ItemId));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task PageBulkManageableAsync_WhenBothDoNotCallBoundsAreChosen_ReturnsOnlyTheInclusiveWindow()
    {
        // Arrange
        // Each bound is added as its own clause, so a window is only ever the two of them meeting. Asserting them
        // together is what catches one bound overwriting the other's parameter or clause and quietly widening the
        // window on both sides — the shape of mistake that a single-bound test cannot see.
        var databasePath = DatabasePath("do-not-call-window");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            var start = _now.AddDays(-1);
            var end = _now;
            string beforeItemId;
            string startItemId;
            string endItemId;
            string afterItemId;

            await using (var seedSession = store.CreateSession())
            {
                beforeItemId = await SaveOptedOutContactActivityAsync(seedSession, _now.AddDays(-2));
                startItemId = await SaveOptedOutContactActivityAsync(seedSession, start);
                endItemId = await SaveOptedOutContactActivityAsync(seedSession, end);
                afterItemId = await SaveOptedOutContactActivityAsync(seedSession, _now.AddDays(1));
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var querySession = store.CreateSession();
            var activityStore = CreateActivityStore(querySession, store, connectionString);

            // Act
            var result = await activityStore.PageBulkManageableAsync(
                1,
                10,
                new BulkManageActivityFilter
                {
                    DoNotCallFrom = start,
                    DoNotCallTo = end,
                },
                TestContext.Current.CancellationToken);

            // Assert
            var matchedItemIds = result.Entries.Select(activity => activity.ItemId).ToArray();

            Assert.Equal(2, result.Count);
            Assert.Equal(
                new[] { startItemId, endItemId }.OrderBy(itemId => itemId, StringComparer.Ordinal),
                matchedItemIds.OrderBy(itemId => itemId, StringComparer.Ordinal));
            Assert.DoesNotContain(beforeItemId, matchedItemIds);
            Assert.DoesNotContain(afterItemId, matchedItemIds);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task PageBulkManageableAsync_WhenNoDoNotCallFilterIsChosen_StillReturnsContactsWhoNeverOptedOut()
    {
        // Arrange
        // The same guard as the statement-shape test, asserted through the database this time, because the harm is
        // in the rows rather than the text. Most contacts have no communication-preference row at all, so a stray
        // INNER JOIN does not merely narrow the result — it removes nearly everybody, and what is left is exactly
        // the population that must never be dialled or messaged again.
        var databasePath = DatabasePath("no-do-not-call-filter");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            var optedOutContactId = IdGenerator.GenerateId();
            var reachableContactId = IdGenerator.GenerateId();
            var neverAskedContactId = IdGenerator.GenerateId();
            string optedOutItemId;
            string reachableItemId;
            string neverAskedItemId;

            await using (var seedSession = store.CreateSession())
            {
                optedOutItemId = await SaveActivityAsync(seedSession, optedOutContactId);
                reachableItemId = await SaveActivityAsync(seedSession, reachableContactId);
                neverAskedItemId = await SaveActivityAsync(seedSession, neverAskedContactId);
                await SavePreferenceAsync(seedSession, optedOutContactId, doNotCall: true, _now.AddDays(-5));
                await SavePreferenceAsync(seedSession, reachableContactId, doNotCall: false, doNotCallUtc: null);
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var querySession = store.CreateSession();
            var activityStore = CreateActivityStore(querySession, store, connectionString);

            // Act
            var result = await activityStore.PageBulkManageableAsync(
                1,
                10,
                new BulkManageActivityFilter
                {
                    Channel = OmnichannelConstants.Channels.Phone,
                },
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Equal(
                new[] { optedOutItemId, reachableItemId, neverAskedItemId }.OrderBy(itemId => itemId, StringComparer.Ordinal),
                result.Entries.Select(activity => activity.ItemId).OrderBy(itemId => itemId, StringComparer.Ordinal));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task PageBulkManageableAsync_WhenTheContactHasMoreThanOnePreferenceRow_CountsTheActivityOnce()
    {
        // Arrange
        // The communication-preference index is mapped from the contact content item with no published-or-latest
        // gate, unlike the contact index the phone and time-zone joins use — which is why those two joins pin
        // Latest and this one has nothing to pin. A contact with a draft alongside its published version therefore
        // has a row per version, and the join multiplies the activity by however many there are. The count is what
        // the confirmation prompt shows before a bulk complete or purge runs, so it has to be the number of
        // activities, not the number of index rows behind them.
        var databasePath = DatabasePath("do-not-call-versions");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            var contactContentItemId = IdGenerator.GenerateId();
            string itemId;

            await using (var seedSession = store.CreateSession())
            {
                itemId = await SaveActivityAsync(seedSession, contactContentItemId);
                await SavePreferenceAsync(seedSession, contactContentItemId, doNotCall: true, _now.AddDays(-5));
                await SavePreferenceAsync(seedSession, contactContentItemId, doNotCall: true, _now.AddDays(-5));
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var querySession = store.CreateSession();
            var activityStore = CreateActivityStore(querySession, store, connectionString);

            // Act
            var result = await activityStore.PageBulkManageableAsync(
                1,
                10,
                new BulkManageActivityFilter
                {
                    DoNotCallFrom = _now.AddDays(-30),
                },
                TestContext.Current.CancellationToken);

            // Assert
            var activity = Assert.Single(result.Entries);
            Assert.Equal(itemId, activity.ItemId);
            Assert.Equal(1, result.Count);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static BulkManageActivityFilterContext CreateContext(BulkManageActivityFilter filter, ISqlDialect dialect)
    {
        // A real dialect and a real naming convention, because the assertions are about the text of the statement:
        // a stubbed dialect returns empty strings for every quoted name and would make any text assertion pass.
        // Each caller names its own dialect rather than leaning on a default, so it is obvious which engine a
        // given assertion is about -- some of these statements differ between SQLite and PostgreSQL.

        var tableNameConvention = new IndexTableNameConvention();
        var sqlBuilder = new SqlBuilder(string.Empty, dialect);

        sqlBuilder.Select();
        sqlBuilder.Selector($"{dialect.QuoteForAliasName(ActivityAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.DocumentId))}");
        sqlBuilder.Table(
            tableNameConvention.GetIndexTable(typeof(OmnichannelActivityIndex), OmnichannelConstants.CollectionName),
            ActivityAlias,
            null);

        return new BulkManageActivityFilterContext(
            filter,
            sqlBuilder,
            dialect,
            string.Empty,
            tableNameConvention,
            schema: null,
            activityTableAlias: ActivityAlias);
    }

    private static string AttemptsColumn(ISqlDialect dialect)
        => $"{dialect.QuoteForAliasName(ActivityAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.Attempts))}";

    private static OmnichannelActivityStore CreateActivityStore(ISession session, IStore store, string connectionString)
    {
        return new OmnichannelActivityStore(
            session,
            [],
            [new BulkManageActivityFilterHandler()],
            store,
            new SqliteDbConnectionAccessor(connectionString));
    }

    private static async Task<IStore> CreateStoreAsync(string connectionString)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite(connectionString));
        store.RegisterIndexes(
        [
            new OmnichannelActivityIndexProvider(),
            new ContactCommunicationPreferenceIndexProvider(),
        ]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(
            OmnichannelConstants.CollectionName,
            TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<OmnichannelActivityIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<ActivityKind>("Kind")
            .Column<string>("Source", column => column.WithLength(50))
            .Column<string>("Channel", column => column.WithLength(50))
            .Column<string>("ChannelEndpointId", column => column.WithLength(26))
            .Column<string>("PreferredDestination", column => column.WithLength(255))
            .Column<string>("ContactContentItemId", column => column.WithLength(26))
            .Column<string>("ContactContentType", column => column.WithLength(255))
            .Column<string>("CampaignId", column => column.WithLength(26))
            .Column<string>("SubjectContentType", column => column.WithLength(26))
            .Column<DateTime>("ScheduledUtc", column => column.NotNull())
            .Column<DateTime>("CompletedUtc")
            .Column<int>("Attempts", column => column.NotNull())
            .Column<string>("AssignedToId", column => column.WithLength(26))
            .Column<DateTime>("AssignedToUtc")
            .Column<ActivityAssignmentStatus>("AssignmentStatus")
            .Column<string>("ReservationId", column => column.WithLength(26))
            .Column<string>("ReservedById", column => column.WithLength(26))
            .Column<DateTime>("ReservedUtc")
            .Column<DateTime>("ReservationExpiresUtc")
            .Column<string>("CreatedById", column => column.WithLength(26))
            .Column<string>("DispositionId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<ActivityUrgencyLevel>("UrgencyLevel")
            .Column<ActivityStatus>("Status")
            .Column<ActivityInteractionType>("InteractionType")
            .Column<bool>("AiEscalated"),
            collection: OmnichannelConstants.CollectionName);

        // Declared exactly as the shipped migration declares it, so the do-not-call flag is a bool column here for
        // the same reason it is one in production. A column hand-declared as an int would hide the very mismatch
        // the PostgreSQL portability test is about.
        await schemaBuilder.CreateMapIndexTableAsync<OmnichannelContactCommunicationPreferenceIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<bool>("DoNotCall", column => column.NotNull().WithDefault(false))
            .Column<DateTime>("DoNotCallUtc")
            .Column<bool>("DoNotSms", column => column.NotNull().WithDefault(false))
            .Column<DateTime>("DoNotSmsUtc")
            .Column<bool>("DoNotEmail", column => column.NotNull().WithDefault(false))
            .Column<DateTime>("DoNotEmailUtc"));

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static async Task<string> SaveOptedOutContactActivityAsync(ISession session, DateTime doNotCallUtc)
    {
        var contactContentItemId = IdGenerator.GenerateId();
        var itemId = await SaveActivityAsync(session, contactContentItemId);

        await SavePreferenceAsync(session, contactContentItemId, doNotCall: true, doNotCallUtc);

        return itemId;
    }

    private static async Task<string> SaveActivityAsync(ISession session, string contactContentItemId)
    {
        var itemId = IdGenerator.GenerateId();

        await session.SaveAsync(
            new OmnichannelActivity
            {
                ItemId = itemId,
                Channel = OmnichannelConstants.Channels.Phone,
                ChannelEndpointId = "endpoint",
                ContactContentItemId = contactContentItemId,
                ContactContentType = "Lead",
                SubjectContentType = "LeadFollowUp",
                PreferredDestination = "+15555550100",
                ScheduledUtc = _now,
                CreatedUtc = _now,
                InteractionType = ActivityInteractionType.Manual,
                Status = ActivityStatus.NotStated,
            },
            collection: OmnichannelConstants.CollectionName,
            cancellationToken: TestContext.Current.CancellationToken);

        return itemId;
    }

    private static Task SavePreferenceAsync(ISession session, string contactContentItemId, bool doNotCall, DateTime? doNotCallUtc)
    {
        return session.SaveAsync(
            new ContactCommunicationPreference
            {
                ContentItemId = contactContentItemId,
                DoNotCall = doNotCall,
                DoNotCallUtc = doNotCallUtc,
            },
            cancellationToken: TestContext.Current.CancellationToken);
    }

    private static string DatabasePath(string purpose)
        => Path.Combine(Path.GetTempPath(), $"bulk-manage-activity-filter-{purpose}-{Guid.NewGuid():N}.db");

    /// <summary>
    /// Stands in for the contact content item that carries the communication preferences. The real index is mapped
    /// from a content item through a whole content-management stack that has nothing to do with the statement under
    /// test; what matters here is that the rows in the index table are the rows production would have.
    /// </summary>
    internal sealed class ContactCommunicationPreference
    {
        public long Id { get; set; }

        public string ContentItemId { get; set; }

        public bool DoNotCall { get; set; }

        public DateTime? DoNotCallUtc { get; set; }
    }

    private sealed class ContactCommunicationPreferenceIndexProvider : IndexProvider<ContactCommunicationPreference>
    {
        public override void Describe(DescribeContext<ContactCommunicationPreference> context)
        {
            context
                .For<OmnichannelContactCommunicationPreferenceIndex>()
                .Map(preference => new OmnichannelContactCommunicationPreferenceIndex
                {
                    ContentItemId = preference.ContentItemId,
                    DoNotCall = preference.DoNotCall,
                    DoNotCallUtc = preference.DoNotCallUtc,
                });
        }
    }

    /// <summary>
    /// Names index and document tables the way YesSql's own convention does, so the statement the handler builds
    /// carries real table names. A stubbed convention returns null for every table and leaves a statement that
    /// could never run, which any assertion about its text would then pass against.
    /// </summary>
    private sealed class IndexTableNameConvention : ITableNameConvention
    {
        public string GetDocumentTable(string collection = null)
            => string.IsNullOrEmpty(collection) ? "Document" : $"{collection}_Document";

        public string GetIndexTable(Type type, string collection = null)
            => string.IsNullOrEmpty(collection) ? type.Name : $"{collection}_{type.Name}";
    }

    private sealed class SqliteDbConnectionAccessor : IDbConnectionAccessor
    {
        private readonly string _connectionString;

        public SqliteDbConnectionAccessor(string connectionString)
        {
            _connectionString = connectionString;
        }

        public DbConnection CreateConnection()
            => new SqliteConnection(_connectionString);
    }
}
