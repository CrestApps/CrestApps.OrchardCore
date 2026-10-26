using System.Security.Claims;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers.Core.Permissions;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers.Verifications;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Indexes;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Migrations;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Reports.DataSources;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DataSources;
using OrchardCore.Security;
using OrchardCore.Settings;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.PhoneNumbers.Verifications;

/// <summary>
/// The phone number verifications data source reads real content items from a SQLite store indexed by the module's own
/// index provider and migration, so the index queries, the fields they expose, the push-down of join keys and
/// statuses, the grouping in the database, and the permission they require are all exercised end to end.
/// </summary>
public sealed class PhoneNumberVerificationReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Compliance");
    private static readonly DateTime _start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"phone-verification-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new PhoneNumberVerificationPartIndexProvider()]);

        await _store.InitializeAsync(cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);
            var migrations = new PhoneNumberVerificationsMigrations(Mock.Of<IContentDefinitionManager>(), NullLogger<PhoneNumberVerificationsMigrations>.Instance)
            {
                SchemaBuilder = schemaBuilder,
            };

            await migrations.CreateAsync();
            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            // A verified mobile number, an older version of the same record, a number whose verification failed three
            // times in a row, a number still waiting, and a record without a number to verify.
            var verified = Contact("contact-verified", "Ada");
            verified.AlterPhoneNumberVerificationResult(new PhoneNumberVerificationResult
            {
                PhoneNumber = "+14255551212",
                NormalizedPhoneNumber = "+14255551212",
                Status = PhoneNumberVerificationStatus.Verified,
                IsValid = true,
                IsMobile = true,
                LineType = PhoneNumberLineType.Mobile,
                CountryCode = "US",
                Carrier = "Contoso Mobile",
                LineStatus = "active",
                MinimumAge = "1y",
                VerificationProvider = "Twilio",
                RawProviderResponse = "raw-secret",
                VerificationDateUtc = _start,
            }, verifiedByUserId: "user-secret");

            var olderVersion = Contact("contact-verified", "Ada (old)", latest: false);
            olderVersion.AlterPhoneNumberVerificationPending("+19995550000");

            var failed = Contact("contact-failed", "Grace");
            failed.AlterPhoneNumberVerificationPending("+12065550100");

            for (var attempt = 0; attempt < 3; attempt++)
            {
                failed.AlterPhoneNumberVerificationResult(new PhoneNumberVerificationResult
                {
                    PhoneNumber = "+12065550100",
                    Status = PhoneNumberVerificationStatus.Failed,
                    VerificationProvider = "Twilio",
                    ErrorMessage = "Rate limited",
                    VerificationDateUtc = _start.AddDays(1 + attempt),
                });
            }

            var pending = Contact("contact-pending", "Linus");
            pending.AlterPhoneNumberVerificationPending("206-555-0101");

            await session.SaveAsync(olderVersion, false, null, cancellationToken);
            await session.SaveAsync(verified, false, null, cancellationToken);
            await session.SaveAsync(failed, false, null, cancellationToken);
            await session.SaveAsync(pending, false, null, cancellationToken);
            await session.SaveAsync(Contact("contact-without-number", "Alan"), false, null, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutTheReportPermission_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, grants: [PhoneNumberVerificationsPermissions.VerifyPhoneNumbers.Name]);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(PhoneNumberVerificationReportDataSets.Verifications, context, TestContext.Current.CancellationToken);
        var table = await ReportDesignerTestServices.ReadAsync(source, Query(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task WithoutAPrincipal_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext();
        var query = new DataSourceQuery
        {
            DataSet = PhoneNumberVerificationReportDataSets.Verifications,
            MaxRows = 100,
            Context = context,
        };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(PhoneNumberVerificationReportDataSets.Verifications, context, TestContext.Current.CancellationToken);
        var table = await ReportDesignerTestServices.ReadAsync(source, query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task Schema_DeclaresKeys_AndNeverExposesTheProviderResponse()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(PhoneNumberVerificationReportDataSets.Verifications, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([PhoneNumberVerificationReportDataSets.Verifications], dataSets.Select(dataSet => dataSet.Name));
        Assert.Equal(PhoneNumberVerificationReportDataSets.Source, PhoneNumberVerificationReportDataSource.Name);
        Assert.Null(schema.DataSet.DefaultDateField);
        Assert.True(schema.FindField(PhoneNumberVerificationReportDataSets.ContentItemIdField).IsIdentifier);
        Assert.True(schema.FindField(PhoneNumberVerificationReportDataSets.ContentItemIdField).IsKeyFilterable);
        Assert.True(schema.FindField(PhoneNumberVerificationReportDataSets.NormalizedPhoneNumberField).IsKeyFilterable);
        Assert.Equal(DataFieldType.DateTime, schema.FindField("LastVerifiedUtc").Type);
        Assert.DoesNotContain(schema.Fields, field => field.Name is "VerificationResultJson" or "RawProviderResponse" or "LastVerifiedByUserId" or "ProviderReferenceId");
    }

    [Fact]
    public async Task Query_ReturnsTheLatestVersionOfEachRecord_WithTypedValues()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["contact-pending", "contact-failed", "contact-verified"], rows.Select(row => row[PhoneNumberVerificationReportDataSets.ContentItemIdField]));

        var pending = rows[0];
        Assert.Equal("206-555-0101", pending["PhoneNumber"]);
        Assert.Equal("Unverified", pending["VerificationStatus"]);
        Assert.Equal(false, pending["IsVerified"]);
        Assert.Null(pending["LineType"]);
        Assert.Equal(false, pending["IsMobile"]);
        Assert.Equal(false, pending["IsExhausted"]);

        var failed = rows[1];
        Assert.Equal("Grace", failed["DisplayText"]);
        Assert.Equal("Failed", failed["VerificationStatus"]);
        Assert.Equal(3L, failed["FailedAttemptCount"]);
        Assert.Equal(3L, failed["VerificationAttemptCount"]);
        Assert.Equal(true, failed["IsExhausted"]);
        Assert.Equal("Rate limited", failed["LastError"]);
        Assert.Null(failed["LastVerifiedUtc"]);
        Assert.Equal(_start.AddDays(3), failed["LastAttemptUtc"]);

        var verified = rows[2];
        Assert.Equal("Ada", verified["DisplayText"]);
        Assert.Equal("Contact", verified["ContentType"]);
        Assert.Equal("+14255551212", verified["PhoneNumber"]);
        Assert.Equal("+14255551212", verified[PhoneNumberVerificationReportDataSets.NormalizedPhoneNumberField]);
        Assert.Equal("US", verified["CountryCode"]);
        Assert.Equal("Verified", verified["VerificationStatus"]);
        Assert.Equal(true, verified["IsVerified"]);
        Assert.Equal("Twilio", verified["VerificationProvider"]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(verified["LastVerifiedUtc"]).Kind);
        Assert.Equal(_start, verified["LastVerifiedUtc"]);
        Assert.Equal(_start.AddDays(PhoneNumberVerificationsSettings.DefaultRevalidationIntervalDays), verified["NextVerificationDueUtc"]);
        Assert.Equal("Mobile", verified["LineType"]);
        Assert.Equal(true, verified["IsMobile"]);
        Assert.Equal(false, verified["IsLandline"]);
        Assert.Equal("Contoso Mobile", verified["Carrier"]);
        Assert.Equal("active", verified["LineStatus"]);
        Assert.Equal("1y", verified["MinimumAge"]);
        Assert.DoesNotContain(rows.SelectMany(row => row.Values), value => value is string text && (text.Contains("secret", StringComparison.Ordinal) || text.Contains("+1999", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Query_ReadsTheSiteSettingsOnlyWhenTheExhaustedFieldIsAsked()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var siteService = SiteService(maxAttempts: 4);
        var source = Source(session, siteService: siteService);

        // Act
        var withoutExhausted = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(PhoneNumberVerificationReportDataSets.ContentItemIdField), TestContext.Current.CancellationToken));
        siteService.Verify(service => service.GetSiteSettingsAsync(), Times.Never);
        var withExhausted = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(PhoneNumberVerificationReportDataSets.ContentItemIdField, "IsExhausted"), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(3, withoutExhausted.Count);
        Assert.All(withExhausted, row => Assert.Equal(false, row["IsExhausted"]));
        siteService.Verify(service => service.GetSiteSettingsAsync(), Times.Once);
    }

    [Fact]
    public async Task Query_PushDownNeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var numbers = Query(PhoneNumberVerificationReportDataSets.ContentItemIdField);
        numbers.Conditions.Add(new DataCondition { Field = PhoneNumberVerificationReportDataSets.NormalizedPhoneNumberField, Operator = DataFilterOperator.In, Values = ["+14255551212", "+12065550100"], IsJoinKey = true });
        var items = Query(PhoneNumberVerificationReportDataSets.ContentItemIdField);
        items.Conditions.Add(new DataCondition { Field = PhoneNumberVerificationReportDataSets.ContentItemIdField, Operator = DataFilterOperator.In, Values = ["contact-pending", "contact-without-number"], IsJoinKey = true });
        var status = Query(PhoneNumberVerificationReportDataSets.ContentItemIdField);
        status.Conditions.Add(Condition("VerificationStatus", DataFilterOperator.In, "failed", "unverified"));
        var provider = Query(PhoneNumberVerificationReportDataSets.ContentItemIdField);
        provider.Conditions.Add(Condition("VerificationProvider", DataFilterOperator.Equals, "TWILIO"));
        var none = Query(PhoneNumberVerificationReportDataSets.ContentItemIdField);
        none.Conditions.Add(new DataCondition { Field = PhoneNumberVerificationReportDataSets.ContentItemIdField, Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });

        // Act
        var numberRows = Rows(await ReportDesignerTestServices.ReadAsync(source, numbers, TestContext.Current.CancellationToken));
        var itemRows = Rows(await ReportDesignerTestServices.ReadAsync(source, items, TestContext.Current.CancellationToken));
        var statusRows = Rows(await ReportDesignerTestServices.ReadAsync(source, status, TestContext.Current.CancellationToken));
        var providerRows = Rows(await ReportDesignerTestServices.ReadAsync(source, provider, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["contact-failed", "contact-verified"], numberRows.Select(row => row[PhoneNumberVerificationReportDataSets.ContentItemIdField]));
        Assert.Equal(["contact-pending"], itemRows.Select(row => row[PhoneNumberVerificationReportDataSets.ContentItemIdField]));
        Assert.Equal(["contact-pending", "contact-failed"], statusRows.Select(row => row[PhoneNumberVerificationReportDataSets.ContentItemIdField]));
        Assert.Equal(["contact-failed", "contact-verified"], providerRows.Select(row => row[PhoneNumberVerificationReportDataSets.ContentItemIdField]));
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Aggregate_CountsByStatusInTheDatabase()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var query = new DataAggregateQuery
        {
            DataSet = PhoneNumberVerificationReportDataSets.Verifications,
            Groups = [new DataAggregateGroup { Field = "VerificationStatus" }, new DataAggregateGroup { Field = "IsMobile" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        // Act
        var groups = await ((IDataAggregateSource)source).AggregateAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(groups);
        var counts = groups.ToDictionary(row => ((string)row[0], Assert.IsType<bool>(row[1])), row => (long)row[2]);
        Assert.Equal(1L, counts[("Verified", true)]);
        Assert.Equal(1L, counts[("Failed", false)]);
        Assert.Equal(1L, counts[("Unverified", false)]);
        Assert.Equal(3, counts.Count);
    }

    [Fact]
    public async Task DataSourcesStartup_RegistersTheDataSource()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var services = new ServiceCollection();

        services.AddSingleton(session);
        services.AddSingleton(SiteService(maxAttempts: 3).Object);
        services.AddSingleton(Authorization([PhoneNumberVerificationsPermissions.RunPhoneNumberVerificationsReport.Name]));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        // Act
        new DataSourcesStartup().ConfigureServices(services);

        await using var provider = services.BuildServiceProvider();
        var registered = Assert.Single(provider.GetRequiredService<IOptions<DataSourceOptions>>().Value.DataSources);
        var dataSets = await provider.GetRequiredService<PhoneNumberVerificationReportDataSource>().GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(typeof(PhoneNumberVerificationReportDataSource), registered.Type);
        Assert.Equal([PhoneNumberVerificationReportDataSets.Verifications], dataSets.Select(dataSet => dataSet.Name));
    }

    private static ContentItem Contact(string contentItemId, string displayText, bool latest = true)
    {
        return new ContentItem
        {
            ContentItemId = contentItemId,
            ContentItemVersionId = $"{contentItemId}-{(latest ? "latest" : "old")}",
            ContentType = "Contact",
            DisplayText = displayText,
            Latest = latest,
            Published = latest,
        };
    }

    private static PhoneNumberVerificationReportDataSource Source(ISession session, string[] grants = null, Mock<ISiteService> siteService = null)
    {
        var authorizationService = Authorization(grants ?? [PhoneNumberVerificationsPermissions.RunPhoneNumberVerificationsReport.Name]);

        siteService ??= SiteService(maxAttempts: 3);

        return new PhoneNumberVerificationReportDataSource(
            new PhoneNumberVerificationsReportDataSet(session, siteService.Object, authorizationService, new PassThroughStringLocalizer<PhoneNumberVerificationsReportDataSet>()));
    }

    private static Mock<ISiteService> SiteService(int maxAttempts)
    {
        var site = new Mock<ISite>();

        site
            .Setup(value => value.GetOrCreate<PhoneNumberVerificationsSettings>())
            .Returns(new PhoneNumberVerificationsSettings { MaxVerificationAttempts = maxAttempts });

        var siteService = new Mock<ISiteService>();

        siteService
            .Setup(service => service.GetSiteSettingsAsync())
            .ReturnsAsync(site.Object);

        return siteService;
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
    {
        return new DataCondition
        {
            Field = field,
            Operator = filterOperator,
            Values = values,
        };
    }

    private static DataSourceQuery Query(params string[] fields)
    {
        return new DataSourceQuery
        {
            DataSet = PhoneNumberVerificationReportDataSets.Verifications,
            Fields = new HashSet<string>(fields, StringComparer.Ordinal),
            MaxRows = 100,
            Context = new DataSourceContext { User = _user },
        };
    }

    private static List<Dictionary<string, object>> Rows(DataRowSet table)
    {
        return table.Rows
            .Select(row => table.Fields.Select((field, index) => (field.Name, Value: row[index])).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal))
            .ToList();
    }
}
