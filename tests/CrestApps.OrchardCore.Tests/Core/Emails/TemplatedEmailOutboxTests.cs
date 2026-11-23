using System.Text.Json;
using CrestApps.OrchardCore.Core.Emails;
using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.WorkQueues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data.Migration;
using OrchardCore.Email;
using OrchardCore.Infrastructure;
using OrchardCore.Modules;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Core.Emails;

/// <summary>
/// The durable templated email outbox on a real store: an email is rendered once with the site's template and sent at
/// once; one the email service doesn't take waits and is tried again after 5 minutes, 15 minutes, an hour, 4 hours and
/// 12 hours, then given up; an email that can't go at all (no email service, no recipient, nothing built) is skipped and
/// not kept; and the observers hear of every attempt.
/// </summary>
public sealed class TemplatedEmailOutboxTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"templated-email-outbox-{Guid.NewGuid():N}.db");
    private readonly MutableClock _clock = new();
    private readonly FakeEmailService _emailService = new();
    private readonly RecordingObserver _observer = new();
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = await CreateStoreAsync(_databasePath);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task AnEmailTheServiceTakes_IsSentNow_WithTheSiteTemplate_AndKeptAsSent()
    {
        // Arrange
        await using var provider = BuildProvider();

        // Act
        var result = await SendAsync(provider, Email());

        // Assert
        Assert.Equal(TemplatedEmailOutboxStatus.Sent, result.Status);
        Assert.False(string.IsNullOrEmpty(result.ItemId));

        var message = Assert.Single(_emailService.Sent);
        Assert.Equal("dana@example.com", message.To);
        Assert.Equal("Template: Welcome", message.Subject);
        Assert.Equal("<div class=\"layout\"><p>Hello</p></div>", message.HtmlBody);

        var stored = await LoadAsync(result.ItemId);
        Assert.Equal(LeasedWorkItemStatus.Completed, stored.Status);
        Assert.Equal(_clock.UtcNow, stored.SentUtc);
        Assert.Equal("Template: Welcome", stored.Subject);
        Assert.Equal(new RecordRef("Member", "m1"), stored.Source);
        Assert.Equal("e1", stored.EmailProperties["Entry"]);

        var (kind, delivery) = Assert.Single(_observer.Calls);
        Assert.Equal("Delivered", kind);
        Assert.Equal(TemplatedEmailOutboxStatus.Sent, delivery.Status);
        Assert.Equal("e1", delivery.Properties["Entry"]);
    }

    [Fact]
    public async Task AnEmailTheServiceRefuses_WaitsFiveMinutes_AndTheObserversHearIt()
    {
        // Arrange
        await using var provider = BuildProvider();
        _emailService.Failure = "Mailbox unavailable";

        // Act
        var result = await SendAsync(provider, Email());

        // Assert
        Assert.Equal(TemplatedEmailOutboxStatus.Queued, result.Status);

        var stored = await LoadAsync(result.ItemId);
        Assert.Equal(LeasedWorkItemStatus.Pending, stored.Status);
        Assert.Equal(1, stored.AttemptCount);
        Assert.Equal(_clock.UtcNow.AddMinutes(5), stored.NextAttemptUtc);
        Assert.Equal("Mailbox unavailable", stored.LastError);
        Assert.Null(stored.SentUtc);

        var (kind, delivery) = Assert.Single(_observer.Calls);
        Assert.Equal("Retrying", kind);
        Assert.Equal(TemplatedEmailOutboxStatus.Queued, delivery.Status);
        Assert.Equal("Mailbox unavailable", delivery.LastError);
        Assert.Equal(_clock.UtcNow.AddMinutes(5), delivery.NextAttemptUtc);
    }

    [Fact]
    public async Task AWaitingEmail_IsNotTriedBeforeItsTime_AndIsSentOnceTheServiceTakesIt()
    {
        // Arrange
        await using var provider = BuildProvider();
        _emailService.Failure = "Mailbox unavailable";
        var result = await SendAsync(provider, Email());
        _emailService.Failure = null;

        // Act
        var early = await SendDueAsync(provider);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(5);
        var due = await SendDueAsync(provider);

        // Assert
        Assert.Equal(0, early.Claimed);
        Assert.Equal((1, 1, 0, 0), (due.Claimed, due.Sent, due.Retried, due.Abandoned));

        var stored = await LoadAsync(result.ItemId);
        Assert.Equal(LeasedWorkItemStatus.Completed, stored.Status);
        Assert.Equal(_clock.UtcNow, stored.SentUtc);
        Assert.Null(stored.LastError);

        // The retry sends the email as it was rendered the first time.
        Assert.Equal("Template: Welcome", Assert.Single(_emailService.Sent).Subject);
        Assert.Equal(["Retrying", "Delivered"], _observer.Calls.Select(call => call.Kind));
    }

    [Fact]
    public void TheRetries_WaitFiveMinutes_FifteenMinutes_AnHour_FourHours_AndTwelveHours()
    {
        Assert.Equal(
            [TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1), TimeSpan.FromHours(4), TimeSpan.FromHours(12)],
            Enumerable.Range(1, 5).Select(TemplatedEmailOutbox.GetRetryDelay));
        Assert.Equal(TimeSpan.FromHours(12), TemplatedEmailOutbox.GetRetryDelay(9));
        Assert.Equal(TimeSpan.FromMinutes(5), TemplatedEmailOutbox.GetRetryDelay(0));
        Assert.Equal(TemplatedEmailOutbox.MaxAttempts, TemplatedEmailOutbox.RetryDelays.Count + 1);
    }

    [Fact]
    public async Task AnEmailThatKeepsFailing_FollowsTheBackoff_AndIsGivenUpAfterSixAttempts()
    {
        // Arrange
        await using var provider = BuildProvider();
        _emailService.Failure = "Mailbox unavailable";
        var result = await SendAsync(provider, Email());
        var waits = new List<TimeSpan>();

        // Act
        for (var retry = 1; retry <= 5; retry++)
        {
            var before = await LoadAsync(result.ItemId);
            waits.Add(before.NextAttemptUtc - _clock.UtcNow);
            _clock.UtcNow = before.NextAttemptUtc;
            await SendDueAsync(provider);
        }

        var afterward = await SendDueAsync(provider);

        // Assert
        Assert.Equal([TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1), TimeSpan.FromHours(4), TimeSpan.FromHours(12)], waits);

        var stored = await LoadAsync(result.ItemId);
        Assert.Equal(LeasedWorkItemStatus.DeadLettered, stored.Status);
        Assert.Equal(6, stored.AttemptCount);
        Assert.Equal(_clock.UtcNow, stored.AbandonedUtc);
        Assert.Equal(0, afterward.Claimed);
        Assert.Empty(_emailService.Sent);
        Assert.Equal(["Retrying", "Retrying", "Retrying", "Retrying", "Retrying", "Abandoned"], _observer.Calls.Select(call => call.Kind));
        Assert.Equal(TemplatedEmailOutboxStatus.Abandoned, _observer.Calls[^1].Delivery.Status);
    }

    [Fact]
    public async Task WithoutAnEmailService_AnEmailIsSkipped_AndNothingIsKept()
    {
        // Arrange
        await using var provider = BuildProvider(withEmailService: false);

        // Act
        var result = await SendAsync(provider, Email());

        // Assert
        Assert.Equal(TemplatedEmailOutboxStatus.Skipped, result.Status);
        Assert.Null(result.ItemId);
        Assert.Equal(0, await CountAsync());
        Assert.Empty(_observer.Calls);
    }

    [Fact]
    public async Task AnEmailWithoutARecipient_OrWhoseBuildGivesNothing_IsSkipped()
    {
        // Arrange
        await using var provider = BuildProvider();

        // Act
        var noRecipient = await SendAsync(provider, new TemplatedEmail { Kind = "welcome", To = " " });
        var turnedOff = await SendAsync(provider, Email(), _ => Task.FromResult<TemplatedEmailContent>(null));

        // Assert
        Assert.Equal(TemplatedEmailOutboxStatus.Skipped, noRecipient.Status);
        Assert.Equal(TemplatedEmailOutboxStatus.Skipped, turnedOff.Status);
        Assert.Empty(_emailService.Sent);
        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task AFailingObserver_NeverStopsTheEmail()
    {
        // Arrange
        await using var provider = BuildProvider(configure: services => services.AddScoped<ITemplatedEmailOutboxObserver, ThrowingObserver>());

        // Act
        var result = await SendAsync(provider, Email());

        // Assert
        Assert.Equal(TemplatedEmailOutboxStatus.Sent, result.Status);
        Assert.Single(_emailService.Sent);
        Assert.Equal("Delivered", Assert.Single(_observer.Calls).Kind);
    }

    [Fact]
    public async Task ARecordsEmails_AndTheEmailsToAnAddress_CanBeRemoved()
    {
        // Arrange
        await using var provider = BuildProvider();
        await SendAsync(provider, Email());
        await SendAsync(provider, new TemplatedEmail { Kind = "welcome", To = "Lee@Example.com", Source = new RecordRef("Member", "m2") });
        await SendAsync(provider, new TemplatedEmail { Kind = "welcome", To = "cy@example.com", Source = new RecordRef("Member", "m3") });

        // Act
        var bySource = await RunAsync(provider, services => services.GetRequiredService<ITemplatedEmailOutboxStore>().DeleteBySourceAsync(new RecordRef("Member", "m1"), TestContext.Current.CancellationToken));
        var byRecipient = await RunAsync(provider, services => services.GetRequiredService<ITemplatedEmailOutboxStore>().DeleteByRecipientAsync(" lee@example.COM", TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(1, bySource);
        Assert.Equal(1, byRecipient);
        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public void RegisteringTheOutboxFromSeveralFeatures_RegistersItOnce()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddTemplatedEmailOutbox();
        services.AddTemplatedEmailOutbox();
        services.AddTemplatedEmailOutboxObserver<RecordingObserver>();
        services.AddTemplatedEmailOutboxObserver<RecordingObserver>();

        // Assert
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ITemplatedEmailOutbox));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ITemplatedEmailOutboxStore));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IIndexProvider) && descriptor.ImplementationType == typeof(TemplatedEmailOutboxIndexProvider));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IDataMigration) && descriptor.ImplementationType == typeof(TemplatedEmailOutboxMigrations));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IBackgroundTask) && descriptor.ImplementationType == typeof(TemplatedEmailOutboxBackgroundTask));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ITemplatedEmailOutboxObserver) && descriptor.ImplementationType == typeof(RecordingObserver));
    }

    [Fact]
    public async Task TheRetentionPolicy_RemovesSentEmailsAfterThirtyDays_AndKeepsTheOnesThatStillWait()
    {
        // Arrange
        await using var provider = BuildProvider();
        var sent = await SendAsync(provider, Email());
        _emailService.Failure = "Mailbox unavailable";
        var waiting = await SendAsync(provider, Email());
        var policy = new TemplatedEmailOutboxRetentionPolicy(session: null);
        Assert.True(policy.TryGetCutoff(_clock.UtcNow.AddDays(TemplatedEmailOutboxRetentionPolicy.RetentionDays + 1), out var cutoff));

        // Act
        var purged = await RunAsync(provider, services => new TemplatedEmailOutboxRetentionPolicy(services.GetRequiredService<ISession>())
            .PurgeBatchAsync(cutoff, 100, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(1, purged);
        Assert.Null(await LoadAsync(sent.ItemId));
        Assert.NotNull(await LoadAsync(waiting.ItemId));
    }

    private static TemplatedEmail Email()
        => new()
        {
            Kind = "welcome",
            To = "dana@example.com",
            Reference = "m1",
            Source = new RecordRef("Member", "m1"),
            Properties = new Dictionary<string, string>(StringComparer.Ordinal) { ["Entry"] = "e1" },
        };

    private static Task<TemplatedEmailContent> Build(CancellationToken cancellationToken)
        => Task.FromResult(new TemplatedEmailContent { Subject = "Welcome", Html = "<p>Hello</p>" });

    private static Task<TemplatedEmailOutboxResult> SendAsync(ServiceProvider provider, TemplatedEmail email, Func<CancellationToken, Task<TemplatedEmailContent>> build = null)
        => RunAsync(provider, services => services.GetRequiredService<ITemplatedEmailOutbox>().SendAsync(email, build ?? new Func<CancellationToken, Task<TemplatedEmailContent>>(Build), TestContext.Current.CancellationToken));

    private static Task<TemplatedEmailOutboxRun> SendDueAsync(ServiceProvider provider)
        => RunAsync(provider, services => services.GetRequiredService<TemplatedEmailOutbox>().SendDueAsync(50, TestContext.Current.CancellationToken));

    // One unit of work, committed at its end, as a request or a background task's scope is.
    private static async Task<TResult> RunAsync<TResult>(ServiceProvider provider, Func<IServiceProvider, Task<TResult>> work)
    {
        await using var scope = provider.CreateAsyncScope();

        var result = await work(scope.ServiceProvider);

        await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);

        return result;
    }

    private async Task<TemplatedEmailOutboxItem> LoadAsync(string itemId)
    {
        await using var session = _store.CreateSession();

        return await session.Query<TemplatedEmailOutboxItem, TemplatedEmailOutboxIndex>(index => index.ItemId == itemId, collection: TemplatedEmailOutboxStorage.CollectionName)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> CountAsync()
    {
        await using var session = _store.CreateSession();

        return await session.Query<TemplatedEmailOutboxItem, TemplatedEmailOutboxIndex>(collection: TemplatedEmailOutboxStorage.CollectionName)
            .CountAsync(TestContext.Current.CancellationToken);
    }

    private ServiceProvider BuildProvider(bool withEmailService = true, Action<IServiceCollection> configure = null)
    {
        var services = new ServiceCollection();

        services.AddSingleton(_store);
        services.AddScoped(_ => _store.CreateSession());
        services.AddSingleton<IClock>(_clock);
        services.AddLogging();
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughLocalizer<>));
        services.AddSingleton<ITemplatedEmailTemplates, LayoutTemplates>();
        services.AddSingleton<ITemplatedEmailOutboxObserver>(_observer);

        if (withEmailService)
        {
            services.AddSingleton<IEmailService>(_emailService);
        }

        configure?.Invoke(services);
        services.AddTemplatedEmailOutbox();

        return services.BuildServiceProvider();
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration =>
        {
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False");
            configuration.ContentSerializer = new global::YesSql.Serialization.DefaultContentJsonSerializer(JOptions.Default);
        });

        store.RegisterIndexes(new IIndexProvider[] { new TemplatedEmailOutboxIndexProvider() });

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(TemplatedEmailOutboxStorage.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await TemplatedEmailOutboxSchema.CreateAsync(new SchemaBuilder(store.Configuration, transaction));
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    // The site's template: its own subject and a layout around the body.
    private sealed class LayoutTemplates : ITemplatedEmailTemplates
    {
        public Task<TemplatedEmailMessage> ApplyAsync(TemplatedEmailContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(new TemplatedEmailMessage("Template: " + context.Subject, context.Text, "<div class=\"layout\">" + context.Html + "</div>"));
    }

    private sealed class FakeEmailService : IEmailService
    {
        public List<MailMessage> Sent { get; } = [];

        // What the service answers: null takes the email, otherwise it refuses it with this error.
        public string Failure { get; set; }

        public Task<Result> SendAsync(MailMessage message, string providerName = null, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                return Task.FromResult(Result.Failed(new LocalizedString(Failure, Failure)));
            }

            Sent.Add(message);

            return Task.FromResult(Result.Success());
        }
    }

    private sealed class RecordingObserver : ITemplatedEmailOutboxObserver
    {
        public List<(string Kind, TemplatedEmailDelivery Delivery)> Calls { get; } = [];

        public Task DeliveredAsync(TemplatedEmailDelivery delivery, CancellationToken cancellationToken = default)
            => RecordAsync("Delivered", delivery);

        public Task RetryingAsync(TemplatedEmailDelivery delivery, CancellationToken cancellationToken = default)
            => RecordAsync("Retrying", delivery);

        public Task AbandonedAsync(TemplatedEmailDelivery delivery, CancellationToken cancellationToken = default)
            => RecordAsync("Abandoned", delivery);

        private Task RecordAsync(string kind, TemplatedEmailDelivery delivery)
        {
            Calls.Add((kind, delivery));

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingObserver : ITemplatedEmailOutboxObserver
    {
        public Task DeliveredAsync(TemplatedEmailDelivery delivery, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Observer down");

        public Task RetryingAsync(TemplatedEmailDelivery delivery, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Observer down");

        public Task AbandonedAsync(TemplatedEmailDelivery delivery, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Observer down");
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }

    private sealed class MutableClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

        public ITimeZone[] GetTimeZones() => [];

        public ITimeZone GetTimeZone(string timeZoneId) => null;

        public ITimeZone GetSystemTimeZone() => null;

        public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, ITimeZone timeZone) => dateTimeOffset;
    }
}
