using System.Text.Json;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.PhoneNumbers.Core.Indexes;
using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.AI;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Indexes;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using IStore = YesSql.IStore;
using ISession = YesSql.ISession;
using StoreFactory = YesSql.StoreFactory;

namespace CrestApps.OrchardCore.Tests.Modules.PhoneNumbers.Verifications.AI;

/// <summary>
/// The phone verification queue tools search, filter and count the records as the queue page does, on a real store, and
/// re-queue records for the revalidation task without calling a provider during the chat; both refuse a caller without
/// the queue page's permission.
/// </summary>
public sealed class PhoneNumberVerificationToolTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"phone-verification-tools-{Guid.NewGuid():N}.db");
    private readonly List<PhoneNumberVerificationProviderDescriptor> _enabledProviders = [new("Twilio")];
    private IStore _store;

    public static TheoryData<string, string> ToolPermissions => new()
    {
        { ListPhoneNumberVerificationsTool.TheName, "RunPhoneNumberVerificationsReport" },
        { RetryPhoneNumberVerificationsTool.TheName, "VerifyPhoneNumbers" },
    };

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new ContentItemIndexProvider(), new PhoneNumberVerificationPartIndexProvider()]);
        await _store.InitializeAsync(TestContext.Current.CancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await schemaBuilder.CreateMapIndexTableAsync<ContentItemIndex>(table => table
                .Column<string>("ContentItemId", column => column.WithLength(26))
                .Column<string>("ContentItemVersionId", column => column.WithLength(26))
                .Column<bool>("Published")
                .Column<bool>("Latest")
                .Column<string>("ContentType", column => column.WithLength(255))
                .Column<DateTime>("ModifiedUtc")
                .Column<DateTime>("PublishedUtc")
                .Column<DateTime>("CreatedUtc")
                .Column<string>("Owner", column => column.WithLength(255))
                .Column<string>("Author", column => column.WithLength(255))
                .Column<string>("DisplayText", column => column.WithLength(255)));

            await schemaBuilder.CreateMapIndexTableAsync<PhoneNumberVerificationPartIndex>(table => table
                .Column<string>("ContentItemId", column => column.WithLength(26))
                .Column<string>("PhoneNumber", column => column.WithLength(50))
                .Column<string>("NormalizedPhoneNumber", column => column.WithLength(30))
                .Column<bool>("IsVerified", column => column.NotNull().WithDefault(false))
                .Column<PhoneNumberVerificationStatus>("VerificationStatus")
                .Column<string>("VerificationProvider", column => column.WithLength(64))
                .Column<DateTime>("LastVerifiedUtc")
                .Column<DateTime>("NextVerificationDueUtc")
                .Column<int>("FailedAttemptCount", column => column.NotNull().WithDefault(0))
                .Column<DateTime>("LastAttemptUtc")
                .Column<string>("CountryCode", column => column.WithLength(2))
                .Column<string>("Carrier", column => column.WithLength(128))
                .Column<bool>("IsMobile", column => column.NotNull().WithDefault(false))
                .Column<bool>("IsLandline", column => column.NotNull().WithDefault(false))
                .Column<bool>("IsVoip", column => column.NotNull().WithDefault(false))
                .Column<string>("LineStatus", column => column.WithLength(32)));

            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            await session.SaveAsync(Contact("verified", "+15125550100", PhoneNumberVerificationStatus.Verified, failedAttempts: 0), cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveAsync(Contact("failed", "+15125550101", PhoneNumberVerificationStatus.Failed, failedAttempts: 1, lastError: "Twilio answered 503 for https://lookups.example/v2?key=SECRET-KEY-123"), cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveAsync(Contact("exhausted", "+15125550102", PhoneNumberVerificationStatus.Failed, failedAttempts: 3), cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Theory]
    [MemberData(nameof(ToolPermissions))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName, string permission)
    {
        // Act
        var result = await RunAsync(toolName, new() { ["allFailed"] = true }, signedIn: false);

        // Assert
        Assert.NotNull(permission);
        Assert.Equal(PhoneNumberVerificationsAIToolBase.NotAllowedMessage, result.Text);
    }

    [Theory]
    [MemberData(nameof(ToolPermissions))]
    public async Task Tool_RefusesACallerWithoutTheQueuePagesPermission(string toolName, string permission)
    {
        // Act
        var result = await RunAsync(toolName, toolName == RetryPhoneNumberVerificationsTool.TheName ? new() { ["allFailed"] = true } : new(), authorize: (asked, _) => asked.Name != permission);

        // Assert
        Assert.Equal(PhoneNumberVerificationsAIToolBase.NotAllowedMessage, result.Text);
        Assert.Equal(permission, Assert.Single(result.Checks).Permission);
        Assert.Equal(PhoneNumberVerificationStatus.Failed, await ReadStatusAsync("failed"));
    }

    [Fact]
    public async Task ListPhoneNumberVerifications_FiltersCountsAndHidesKeys()
    {
        // Act
        var failed = JsonElement.Parse((await RunAsync(ListPhoneNumberVerificationsTool.TheName, new() { ["status"] = "Failed" })).Text);
        var attention = JsonElement.Parse((await RunAsync(ListPhoneNumberVerificationsTool.TheName, new() { ["status"] = "needsattention" })).Text);
        var byNumber = JsonElement.Parse((await RunAsync(ListPhoneNumberVerificationsTool.TheName, new() { ["number"] = "0100" })).Text);

        // Assert
        var record = Assert.Single(failed.GetProperty("items").EnumerateArray());

        Assert.Equal("failed", record.GetProperty("contentItemId").GetString());
        Assert.DoesNotContain("SECRET-KEY-123", record.GetProperty("lastError").GetString(), StringComparison.Ordinal);
        Assert.Equal(3, failed.GetProperty("counts").GetProperty("All").GetInt32());
        Assert.Equal(1, failed.GetProperty("counts").GetProperty("NeedsAttention").GetInt32());
        Assert.Equal("exhausted", Assert.Single(attention.GetProperty("items").EnumerateArray()).GetProperty("contentItemId").GetString());
        Assert.Equal("verified", Assert.Single(byNumber.GetProperty("items").EnumerateArray()).GetProperty("contentItemId").GetString());
    }

    [Fact]
    public async Task ListPhoneNumberVerifications_RefusesAnUnknownStatus()
    {
        // Act
        var result = await RunAsync(ListPhoneNumberVerificationsTool.TheName, new() { ["status"] = "Broken" });

        // Assert
        Assert.StartsWith("Nothing was listed: 'Broken' is not a status.", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RetryPhoneNumberVerifications_RequeuesEveryFailedRecord_ForTheRevalidationTask()
    {
        // Act
        var json = JsonElement.Parse((await RunAsync(RetryPhoneNumberVerificationsTool.TheName, new() { ["allFailed"] = true })).Text);

        // Assert
        Assert.Equal(2, json.GetProperty("queued").GetInt32());
        Assert.Equal(PhoneNumberVerificationStatus.Unverified, await ReadStatusAsync("failed"));
        Assert.Equal(PhoneNumberVerificationStatus.Unverified, await ReadStatusAsync("exhausted"));
        Assert.Equal(PhoneNumberVerificationStatus.Verified, await ReadStatusAsync("verified"));
    }

    [Fact]
    public async Task RetryPhoneNumberVerifications_ReportsTheIdsThatAreNotRecords()
    {
        // Act
        var json = JsonElement.Parse((await RunAsync(RetryPhoneNumberVerificationsTool.TheName, new() { ["contentItemIds"] = new[] { "exhausted", "missing" } })).Text);

        // Assert
        Assert.Equal(1, json.GetProperty("queued").GetInt32());
        Assert.Equal("missing", Assert.Single(json.GetProperty("notFound").EnumerateArray()).GetString());
        Assert.Equal(PhoneNumberVerificationStatus.Failed, await ReadStatusAsync("failed"));
    }

    [Fact]
    public async Task RetryPhoneNumberVerifications_RefusesWhenNoProviderIsEnabled()
    {
        // Arrange
        _enabledProviders.Clear();

        // Act
        var result = await RunAsync(RetryPhoneNumberVerificationsTool.TheName, new() { ["allFailed"] = true });

        // Assert
        Assert.StartsWith("Nothing was queued: no phone number verification provider is enabled.", result.Text, StringComparison.Ordinal);
        Assert.Equal(PhoneNumberVerificationStatus.Failed, await ReadStatusAsync("failed"));
    }

    private static ContentItem Contact(string id, string number, PhoneNumberVerificationStatus status, int failedAttempts, string lastError = null)
    {
        var contact = new ContentItem
        {
            ContentItemId = id,
            ContentItemVersionId = id + "-v1",
            ContentType = "Contact",
            DisplayText = "Contact " + id,
            Published = true,
            Latest = true,
            CreatedUtc = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc),
        };

        contact.Alter<PhoneNumberVerificationPart>(part =>
        {
            part.PhoneNumber = number;
            part.NormalizedPhoneNumber = number;
            part.VerificationStatus = status;
            part.FailedAttemptCount = failedAttempts;
            part.LastError = lastError;
            part.LastAttemptUtc = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
        });

        return contact;
    }

    private async Task<PhoneNumberVerificationStatus> ReadStatusAsync(string contentItemId)
    {
        await using var session = _store.CreateSession();
        var item = await session.Query<ContentItem, ContentItemIndex>(index => index.ContentItemId == contentItemId && index.Latest).FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        return item.TryGet<PhoneNumberVerificationPart>(out var part) ? part.VerificationStatus : default;
    }

    private async Task<(string Text, List<(string Permission, object Resource)> Checks)> RunAsync(
        string toolName,
        Dictionary<string, object> arguments,
        bool signedIn = true,
        Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddScoped(_ => _store.CreateSession());

        var site = new Mock<ISite>();
        site.Setup(item => item.GetOrCreate<PhoneNumberVerificationsSettings>()).Returns(new PhoneNumberVerificationsSettings { MaxVerificationAttempts = 3 });

        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);
        services.AddSingleton(siteService.Object);

        var manager = new Mock<IPhoneNumberVerificationManager>();
        manager.Setup(item => item.GetEnabledProvidersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _enabledProviders.ToArray());
        services.AddSingleton(manager.Object);
        services.AddScoped<PhoneNumberVerificationRecordsService>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        AIFunction tool = toolName == ListPhoneNumberVerificationsTool.TheName
            ? new ListPhoneNumberVerificationsTool()
            : new RetryPhoneNumberVerificationsTool();

        var text = await AgentToolTestContext.InvokeAsync(tool, scope.ServiceProvider, arguments);

        await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);

        return (text, provider.GetRequiredService<FakeAuthorizationService>().Checks);
    }
}
