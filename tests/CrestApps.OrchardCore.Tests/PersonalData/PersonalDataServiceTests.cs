using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.PersonalData.Core;
using CrestApps.OrchardCore.PersonalData.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.PersonalData;

/// <summary>
/// One personal-data service runs every module's contributor: it identifies the person through all of them first, so
/// an email one module knows lets another module find its records, then exports or anonymizes in order.
/// </summary>
public sealed class PersonalDataServiceTests
{
    private static readonly DateTime _now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ExportAsync_FindsRecordsByAnEmailAnotherContributorIdentified()
    {
        // Arrange
        var service = CreateService(
            new EmailFinderContributor("user-1", "ann@example.com"),
            new ByEmailContributor("ann@example.com", "Booking"));

        // Act
        var export = await service.ExportAsync(new PersonalDataSubject(userId: "user-1"), TestContext.Current.CancellationToken);

        // Assert
        var record = Assert.Single(export.Records);
        Assert.Equal("Booking", record.Kind);
        Assert.Equal(["ann@example.com"], export.Emails);
        Assert.Equal(_now, export.ExportedUtc);
    }

    [Fact]
    public async Task AnonymizeAsync_IdentifiesEveryoneBeforeAnythingChanges_AndRunsInOrder()
    {
        // Arrange
        var log = new List<string>();
        var service = CreateService(
            new ByEmailContributor("ann@example.com", "Late", order: 10, log),
            new EmailFinderContributor("user-1", "ann@example.com", log),
            new ByEmailContributor("ann@example.com", "Early", order: -10, log));

        // Act
        var result = await service.AnonymizeAsync(new PersonalDataSubject(userId: "user-1"), "Anonymous", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, result.Records["Early"]);
        Assert.Equal(1, result.Records["Late"]);
        Assert.Equal(2, result.Total);

        var firstAnonymize = log.FindIndex(entry => entry.StartsWith("anonymize", StringComparison.Ordinal));
        var lastIdentify = log.FindLastIndex(entry => entry.StartsWith("identify", StringComparison.Ordinal));
        Assert.True(lastIdentify < firstAnonymize);
        Assert.Equal(["anonymize:Early", "anonymize:Finder", "anonymize:Late"], log.Where(entry => entry.StartsWith("anonymize", StringComparison.Ordinal)));
    }

    [Fact]
    public void Subject_MatchesPhoneNumbersByDigits_AndScrubsLongerTermsFirst()
    {
        // Arrange
        var subject = new PersonalDataSubject(email: "ann@example.com", phoneNumber: "+1 (702) 555-0100");
        subject.AddName("Ann");
        subject.AddName("Ann Lee");

        // Act
        var scrubbed = subject.Scrub("Call Ann Lee on +1 (702) 555-0100 or ann@example.com", "[removed]");

        // Assert
        Assert.True(subject.IsPhoneNumber("+17025550100"));
        Assert.False(subject.IsPhoneNumber("+17025550199"));
        Assert.True(subject.IsEmail("ANN@example.com"));
        Assert.Equal("Call [removed] on [removed] or [removed]", scrubbed);
    }

    [Fact]
    public void Subject_RequiresAUserIdEmailOrPhone()
    {
        Assert.Throws<ArgumentException>(() => new PersonalDataSubject());
    }

    [Fact]
    public async Task AddPersonalDataContributor_RegistersTheServiceOnce_AndEveryContributor()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddSingleton(Clock())
            .AddPersonalDataContributor<EmptyContributor>()
            .AddPersonalDataContributor<EmptyContributor>()
            .AddPersonalDataContributor<OtherEmptyContributor>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        // Act
        var contributors = scope.ServiceProvider.GetServices<IPersonalDataContributor>().ToArray();
        var service = scope.ServiceProvider.GetRequiredService<IPersonalDataService>();

        // Assert
        Assert.Equal(2, contributors.Length);
        Assert.IsType<DefaultPersonalDataService>(service);
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IPersonalDataService));
    }

    private static DefaultPersonalDataService CreateService(params IPersonalDataContributor[] contributors)
        => new(contributors, Clock());

    private static IClock Clock()
    {
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        return clock.Object;
    }

    private sealed class EmailFinderContributor : IPersonalDataContributor
    {
        private readonly string _userId;
        private readonly string _email;
        private readonly List<string> _log;

        public EmailFinderContributor(string userId, string email, List<string> log = null)
        {
            _userId = userId;
            _email = email;
            _log = log ?? [];
        }

        public Task IdentifyAsync(PersonalDataSubject subject, CancellationToken cancellationToken = default)
        {
            _log.Add("identify:Finder");

            if (subject.IsUser(_userId))
            {
                subject.AddEmail(_email);
            }

            return Task.CompletedTask;
        }

        public Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
        {
            _log.Add("anonymize:Finder");

            return Task.CompletedTask;
        }
    }

    private sealed class ByEmailContributor : IPersonalDataContributor
    {
        private readonly string _email;
        private readonly string _kind;
        private readonly List<string> _log;

        public ByEmailContributor(string email, string kind, int order = 0, List<string> log = null)
        {
            _email = email;
            _kind = kind;
            Order = order;
            _log = log ?? [];
        }

        public int Order { get; }

        public Task IdentifyAsync(PersonalDataSubject subject, CancellationToken cancellationToken = default)
        {
            _log.Add($"identify:{_kind}");

            return Task.CompletedTask;
        }

        public Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
        {
            if (context.Subject.IsEmail(_email))
            {
                context.Add(_kind, "r1", _kind);
            }

            return Task.CompletedTask;
        }

        public Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
        {
            _log.Add($"anonymize:{_kind}");

            if (context.Subject.IsEmail(_email))
            {
                context.Result.Count(_kind);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class EmptyContributor : IPersonalDataContributor
    {
        public Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class OtherEmptyContributor : IPersonalDataContributor
    {
        public Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
