using System.Security.Claims;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.MailingLists;
using CrestApps.OrchardCore.MailingLists.Core.Indexes;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Reports;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Moq;
using OrchardCore.DataSources;
using OrchardCore.Security;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.MailingLists;

/// <summary>
/// The mailing lists data source reads real members from a SQLite store: only a principal who may see the members of
/// mailing lists reads them, each row carries its list's name and the membership's state, the list and status filters
/// narrow the read in the index without dropping a row, and statuses are counted in the database.
/// </summary>
public sealed class MailingListsReportDataSourceTests : IAsyncLifetime
{
    private const string NewsId = "list-news";
    private const string OffersId = "list-offers";

    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Marketer");
    private static readonly DateTime _start = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-mailing-list-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new MailingListMemberIndexProvider()]);
        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(MailingListsConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);

            await MailingListsIndexSchema.CreateAsync(new SchemaBuilder(_store.Configuration, transaction));
            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            await session.SaveAsync(new MailingListMember
            {
                ItemId = "member-1",
                ListId = NewsId,
                Contact = new ContactInfo { Email = "Ada@Example.com", Name = "Ada" },
                NormalizedEmail = "ada@example.com",
                Status = MailingListMemberStatus.Subscribed,
                Source = MailingListMemberSources.Widget,
                EmailVerifiedUtc = _start.AddMinutes(5),
                SubscribedUtc = _start.AddMinutes(5),
                StatusChangedUtc = _start.AddMinutes(5),
                CouponCodeId = "code-1",
                Consent = new MailingListConsentEvidence { ClientAddress = "203.0.113.5" },
                VerificationNonce = "secret-nonce",
                CreatedUtc = _start,
            }, false, MailingListsConstants.CollectionName, cancellationToken);

            await session.SaveAsync(new MailingListMember
            {
                ItemId = "member-2",
                ListId = NewsId,
                Contact = new ContactInfo { Email = "grace@example.com" },
                NormalizedEmail = "grace@example.com",
                Status = MailingListMemberStatus.Unsubscribed,
                Source = MailingListMemberSources.Import,
                SubscribedUtc = _start.AddDays(1),
                UnsubscribedUtc = _start.AddDays(2),
                StatusChangedUtc = _start.AddDays(2),
                SyncState = MailingListSyncState.Failed,
                SyncError = "Provider said no.",
                CreatedUtc = _start.AddDays(1),
            }, false, MailingListsConstants.CollectionName, cancellationToken);

            await session.SaveAsync(new MailingListMember
            {
                ItemId = "member-3",
                ListId = OffersId,
                Contact = new ContactInfo { Email = "alan@example.com" },
                NormalizedEmail = "alan@example.com",
                Status = MailingListMemberStatus.Pending,
                Source = MailingListMemberSources.Widget,
                UserId = "user-3",
                StatusChangedUtc = _start.AddDays(3),
                CreatedUtc = _start.AddDays(3),
            }, false, MailingListsConstants.CollectionName, cancellationToken);

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutThePermission_OrAUser_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var denied = Source(session, grants: [MailingListsPermissions.ManageMailingListSettings.Name]);
        var allowed = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await denied.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await denied.GetSchemaAsync(MailingListMembersReportDataSet.DataSetName, context, TestContext.Current.CancellationToken);
        var rows = await ReportDesignerTestServices.ReadAsync(denied, Query(), TestContext.Current.CancellationToken);
        var anonymous = await allowed.GetDataSetsAsync(new DataSourceContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(rows.Rows);
        Assert.Empty(anonymous);
    }

    [Fact]
    public async Task Schema_NeverExposesTheEvidenceAddressTheNonceOrProviderErrors()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var schema = await source.GetSchemaAsync(MailingListMembersReportDataSet.DataSetName, new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        var names = schema.Fields.Select(field => field.Name).ToArray();

        Assert.Equal(MailingListsReportDataSource.SourceName, MailingListsReportDataSource.Name);
        Assert.Contains(MailingListMembersReportDataSet.ListField, names);
        Assert.Contains("Email", names);
        Assert.Contains(MailingListMembersReportDataSet.SignedUpUtcField, names);
        Assert.DoesNotContain(names, name => name.Contains("Consent", StringComparison.OrdinalIgnoreCase) || name.Contains("ClientAddress", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Nonce", StringComparison.OrdinalIgnoreCase) || name.Contains("SyncError", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(DataFieldType.DateTime, schema.Fields.Single(field => field.Name == MailingListMembersReportDataSet.SignedUpUtcField).Type);
        Assert.True(schema.Fields.Single(field => field.Name == MailingListMembersReportDataSet.ListIdField).IsIdentifier);
    }

    [Fact]
    public async Task Query_ReturnsEachMember_WithItsListsName_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["member-3", "member-2", "member-1"], rows.Select(row => row[MailingListMembersReportDataSet.ItemIdField]));

        var ada = rows[2];
        Assert.Equal(NewsId, ada[MailingListMembersReportDataSet.ListIdField]);
        Assert.Equal("Weekly news", ada[MailingListMembersReportDataSet.ListField]);
        Assert.Equal("Ada@Example.com", ada["Email"]);
        Assert.Equal("Ada", ada["Name"]);
        Assert.Equal("Subscribed", ada["Status"]);
        Assert.Equal("Widget", ada["Source"]);
        Assert.Equal(true, ada["IsVerified"]);
        Assert.Equal(true, ada["HasCoupon"]);
        Assert.Equal("Synced", ada["SyncState"]);
        Assert.Equal(_start, ada[MailingListMembersReportDataSet.SignedUpUtcField]);

        var grace = rows[1];
        Assert.Equal("Unsubscribed", grace["Status"]);
        Assert.Equal("Failed", grace["SyncState"]);
        Assert.Equal(false, grace["IsVerified"]);
        Assert.Equal(_start.AddDays(2), grace["UnsubscribedUtc"]);

        var alan = rows[0];
        Assert.Equal("Offers", alan[MailingListMembersReportDataSet.ListField]);
        Assert.Equal("user-3", alan["UserId"]);
    }

    [Fact]
    public async Task Query_ByListAndStatus_NarrowsTheRead_AndNeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        var byList = Query();
        byList.Conditions.Add(Condition(MailingListMembersReportDataSet.ListIdField, DataFilterOperator.Equals, NewsId));
        var byStatus = Query();
        byStatus.Conditions.Add(Condition("Status", DataFilterOperator.In, "subscribed", "Pending"));
        var byJoin = Query();
        byJoin.Conditions.Add(new DataCondition { Field = MailingListMembersReportDataSet.ListIdField, Operator = DataFilterOperator.In, Values = [OffersId], IsJoinKey = true });
        var none = Query();
        none.Conditions.Add(new DataCondition { Field = MailingListMembersReportDataSet.ListIdField, Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });

        // Act
        var listRows = Rows(await ReportDesignerTestServices.ReadAsync(source, byList, TestContext.Current.CancellationToken));
        var statusRows = Rows(await ReportDesignerTestServices.ReadAsync(source, byStatus, TestContext.Current.CancellationToken));
        var joinRows = Rows(await ReportDesignerTestServices.ReadAsync(source, byJoin, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["member-2", "member-1"], listRows.Select(row => row[MailingListMembersReportDataSet.ItemIdField]));
        Assert.Equal(["member-3", "member-1"], statusRows.Select(row => row[MailingListMembersReportDataSet.ItemIdField]));
        Assert.Equal(["member-3"], joinRows.Select(row => row[MailingListMembersReportDataSet.ItemIdField]));
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Aggregate_CountsMembersByStatusInTheDatabase()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = new DataAggregateQuery
        {
            DataSet = MailingListMembersReportDataSet.DataSetName,
            Groups = [new DataAggregateGroup { Field = "Status" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        // Act
        var groups = await ((IDataAggregateSource)source).AggregateAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(groups);
        Assert.Equal(3, groups.Count);
        Assert.All(groups, group => Assert.Equal(1L, Convert.ToInt64(group[^1], System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static MailingListsReportDataSource Source(ISession session, string[] grants = null)
    {
        var lists = new Mock<IMailingListManager>();

        lists.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new MailingList { ItemId = NewsId, Name = "news", DisplayText = "Weekly news" },
                new MailingList { ItemId = OffersId, Name = "Offers" },
            ]);

        return new MailingListsReportDataSource(new MailingListMembersReportDataSet(
            session,
            lists.Object,
            Authorization(grants ?? [MailingListsPermissions.ViewMailingListMembers.Name]),
            new PassThroughStringLocalizer<MailingListMembersReportDataSet>()));
    }

    private static IAuthorizationService Authorization(string[] grants)
    {
        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().All(requirement => grants.Contains(requirement.Permission.Name))
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return authorizationService.Object;
    }

    private static DataCondition Condition(string field, DataFilterOperator filterOperator, params object[] values)
        => new()
        {
            Field = field,
            Operator = filterOperator,
            Values = values,
        };

    private static DataSourceQuery Query()
        => new()
        {
            DataSet = MailingListMembersReportDataSet.DataSetName,
            Fields = new HashSet<string>(StringComparer.Ordinal),
            MaxRows = 100,
            Context = new DataSourceContext { User = _user },
        };

    private static List<Dictionary<string, object>> Rows(DataRowSet table)
        => table.Rows
            .Select(row => table.Fields.Select((field, index) => (field.Name, Value: row[index])).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal))
            .ToList();
}
