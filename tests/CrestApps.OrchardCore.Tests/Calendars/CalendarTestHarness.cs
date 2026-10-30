using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Core.Indexes;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using Calendar = CrestApps.OrchardCore.Calendars.Models.Calendar;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// A real SQLite store with the calendar services, for tests that need the managers end to end.
/// </summary>
internal sealed class CalendarTestHarness : IAsyncDisposable
{
    public const string Room = "Room";
    public const string Office = "Office";
    public const string General = "General";
    public const string Personal = "Personal";
    public const string Team = "Team";
    public const string Workforce = "Workforce";

    private readonly string _databasePath;
    private readonly ServiceProvider _provider;

    private CalendarTestHarness(string databasePath, IStore store, ServiceProvider provider)
    {
        _databasePath = databasePath;
        Store = store;
        _provider = provider;
    }

    public IStore Store { get; }

    public static DateTime Now { get; } = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    public static async Task<CalendarTestHarness> CreateAsync()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"calendars-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));

        store.RegisterIndexes([new CalendarIndexProvider(), new CalendarEventIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(CalendarsConstants.CollectionName, TestContext.Current.CancellationToken);

        await using (var session = store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);

            await CalendarIndexSchema.CreateAsync(new SchemaBuilder(store.Configuration, transaction));
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        var services = new ServiceCollection();

        services.AddSingleton(store);
        services.AddScoped(_ => store.CreateSession());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughLocalizer<>));
        services.AddSingleton<IAuthorizationService, ClaimPermissionAuthorizationService>();
        services.AddSingleton<IDistributedLock, NoOpDistributedLock>();

        var clock = new Mock<IClock>();

        clock.SetupGet(c => c.UtcNow).Returns(Now);
        services.AddSingleton(clock.Object);

        var site = new Mock<ISite>();

        site.SetupGet(s => s.TimeZoneId).Returns("America/New_York");

        var siteService = new Mock<ISiteService>();

        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);
        services.AddSingleton(siteService.Object);

        services.AddCalendarsCore();
        services.AddCalendarType(General, type => type.WithCapabilities(CalendarCapabilities.All));
        services.AddCalendarType(Personal, type => type.WithOwner(CalendarOwnerKind.User, onePerOwner: true).WithVisibility(CalendarVisibility.Shared));
        services.AddCalendarType(Team, type => type.WithOwner(CalendarOwnerKind.Role));
        services.AddCalendarType(Room, type => type.WithOwner(CalendarOwnerKind.Resource).LimitConcurrent());
        services.AddCalendarType(Office, type => type
            .LimitConcurrent(2)
            .AddEventType("InOffice", new LocalizationSource("In office"))
            .AddEventType("Closed", new LocalizationSource("Office closed"), e => e.Free()));
        services.AddCalendarType(Workforce, type => type
            .WithOwner(CalendarOwnerKind.Role)
            .AddEventType("Shift", new LocalizationSource("Shift"), e => e.RequiresAttendee())
            .AddEventType("TimeOff", new LocalizationSource("Time off"), e => e.AllDayByDefault().NeedsApproval()));

        return new CalendarTestHarness(databasePath, store, services.BuildServiceProvider());
    }

    public static ClaimsPrincipal User(string userId, params string[] permissionsOrRoles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId), new(ClaimTypes.Name, userId) };

        foreach (var item in permissionsOrRoles)
        {
            claims.Add(item.StartsWith("role:", StringComparison.Ordinal)
                ? new Claim(ClaimTypes.Role, item.Substring(5))
                : new Claim(Permission.ClaimType, item));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    public async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> work, bool commit = true)
    {
        await using var scope = _provider.CreateAsyncScope();
        var result = await work(scope.ServiceProvider);

        if (commit)
        {
            await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return result;
    }

    public Task RunAsync(Func<IServiceProvider, Task> work, bool commit = true)
    {
        return RunAsync<object>(async services =>
        {
            await work(services);

            return null;
        }, commit);
    }

    public AsyncServiceScope CreateScope()
    {
        return _provider.CreateAsyncScope();
    }

    public Task<Calendar> CreateCalendarAsync(string type, string name, Action<Calendar> configure = null)
    {
        return RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ICalendarService>();
            var calendar = await manager.NewAsync(type);

            calendar.Name = name;
            configure?.Invoke(calendar);

            var result = await manager.CreateAsync(calendar);

            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Message)));

            return result.Value;
        });
    }

    public Task<CalendarResult<CalendarEvent>> CreateEventAsync(string calendarId, DateTime start, DateTime end, Action<CalendarEvent> configure = null)
    {
        return RunAsync(async services =>
        {
            var calendars = services.GetRequiredService<ICalendarService>();
            var events = services.GetRequiredService<ICalendarEventManager>();
            var calendar = await calendars.FindByIdAsync(calendarId);
            var calendarEvent = await events.NewAsync(calendar);

            calendarEvent.Title = "Test";
            calendarEvent.AllDay = false;
            calendarEvent.Start = start;
            calendarEvent.End = end;
            configure?.Invoke(calendarEvent);

            return await events.CreateAsync(calendarEvent);
        });
    }

    public Task<IReadOnlyList<CalendarOccurrence>> GetOccurrencesAsync(CalendarOccurrenceQuery query)
    {
        return RunAsync(services => services.GetRequiredService<ICalendarQueryService>().GetOccurrencesAsync(query), commit: false);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        TemporarySqliteDatabase.DisposeAndDelete(Store, _databasePath);
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class NoOpDistributedLock : IDistributedLock
    {
        public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
            => Task.FromResult<ILocker>(new NoOpLocker());

        public Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
            => Task.FromResult<(ILocker, bool)>((new NoOpLocker(), true));

        public Task<bool> IsLockAcquiredAsync(string key)
            => Task.FromResult(false);
    }

    private sealed class NoOpLocker : ILocker
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }

    // Grants a permission when the user has a claim for it or for a permission that implies it.
    private sealed class ClaimPermissionAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            foreach (var requirement in requirements)
            {
                if (requirement is PermissionRequirement permissionRequirement && !Has(user, permissionRequirement.Permission))
                {
                    return Task.FromResult(AuthorizationResult.Failed());
                }
            }

            return Task.FromResult(AuthorizationResult.Success());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());

        private static bool Has(ClaimsPrincipal user, Permission permission)
        {
            if (user.HasClaim(Permission.ClaimType, permission.Name))
            {
                return true;
            }

            return permission.ImpliedBy?.Any(implied => implied is not null && Has(user, implied)) == true;
        }
    }
}
