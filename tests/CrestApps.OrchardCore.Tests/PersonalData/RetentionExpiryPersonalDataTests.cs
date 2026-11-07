using CrestApps.OrchardCore.Commerce.Services;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Handlers;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.PersonalData;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.PersonalData.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.Tests.Customers;
using CrestApps.OrchardCore.Tests.Modules.Omnichannel;
using CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.PersonalData;

/// <summary>
/// The nightly guest expiry reaches only the commerce records it reached before the shared personal-data service:
/// the CRM, Workforce, Scheduling and the other modules' records are only erased by an explicit erasure request.
/// </summary>
public sealed class RetentionExpiryPersonalDataTests : IAsyncLifetime
{
    private static readonly DateTime _now = new(2026, 11, 6, 3, 30, 0, DateTimeKind.Utc);

    private string _path;
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var (connectionString, path) = ContactPhoneTestStore.CreateDatabase("crestapps-pd-expiry");

        _path = path;
        _store = await ContactPhoneTestStore.CreateAsync(connectionString);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task AnExpiredGuest_WhoseEmailMatchesACrmContact_LeavesTheContactUntouched()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var contact = CrmPersonalDataTestContacts.Create("Ann Lee", ["ann@example.com"], ["+17025550100"]);
        await ContactPhoneTestStore.SaveAsync(_store, contact);

        var contentManager = new Mock<IContentManager>();
        var commerce = new Mock<ICommercePersonalDataService>();
        commerce
            .Setup(service => service.ExpireAsync(It.IsAny<CustomerOwner>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommercePersonalDataAnonymizeResult());

        await using var session = _store.CreateSession();
        var privacy = CreatePrivacyService(session, contentManager.Object, commerce.Object);
        var guest = Guest("ann@example.com", "+17025550100");

        // Act
        await privacy.ExpireAsync(guest, cancellationToken);

        // Assert
        contentManager.Verify(manager => manager.UpdateAsync(It.IsAny<ContentItem>()), Times.Never);
        commerce.Verify(service => service.ExpireAsync(null, "ann@example.com", It.IsAny<CancellationToken>()), Times.Once);
        commerce.Verify(service => service.AnonymizeAsync(It.IsAny<CustomerOwner>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(CustomerStatus.Anonymized, guest.Status);
        Assert.Equal(CustomerPrivacyAction.Expired, Assert.Single(guest.PrivacyLog).Action);
    }

    [Fact]
    public async Task AnExplicitErase_StillReachesTheCrmContact()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var contact = CrmPersonalDataTestContacts.Create("Ann Lee", ["ann@example.com"], ["+17025550100"]);
        await ContactPhoneTestStore.SaveAsync(_store, contact);

        var contentManager = new Mock<IContentManager>();
        var commerce = new Mock<ICommercePersonalDataService>();
        commerce
            .Setup(service => service.AnonymizeAsync(It.IsAny<CustomerOwner>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommercePersonalDataAnonymizeResult());

        await using var session = _store.CreateSession();
        var privacy = CreatePrivacyService(session, contentManager.Object, commerce.Object);
        var guest = Guest("ann@example.com", "+17025550100");

        // Act
        await privacy.EraseAsync(guest, "admin", cancellationToken);

        // Assert
        contentManager.Verify(manager => manager.UpdateAsync(It.Is<ContentItem>(item => item.ContentItemId == contact.ContentItemId)), Times.Once);
        commerce.Verify(service => service.AnonymizeAsync(null, "ann@example.com", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExpireAsync_RunsOnlyTheContributorsThatTakePartInRetentionExpiry()
    {
        // Arrange
        var log = new List<string>();
        var service = new DefaultPersonalDataService(
        [
            new RecordingContributor("Crm", takesPart: false, log),
            new RecordingContributor("Commerce", takesPart: true, log),
        ],
        Clock());

        // Act
        var result = await service.ExpireAsync(new PersonalDataSubject(email: "ann@example.com"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["identify:Commerce", "anonymize:Commerce:RetentionExpiry"], log);
        Assert.Equal(1, result.Records["Commerce"]);
        Assert.False(result.Records.ContainsKey("Crm"));
    }

    [Fact]
    public void OnlyTheCommerceBridgeTakesPartInRetentionExpiry_AmongTheModuleContributors()
    {
        // The contributors that are not commerce opt out by leaving the default.
        IPersonalDataContributor commerce = new CommercePersonalDataContributor(Mock.Of<ICommercePersonalDataService>());
        IPersonalDataContributor crm = new CrmContactPersonalDataContributor(null, TestContactMethods.Accessor, Mock.Of<IContentManager>(), new PassThroughStringLocalizer<CrmContactPersonalDataContributor>(), []);

        Assert.True(commerce.TakesPartInRetentionExpiry);
        Assert.False(crm.TakesPartInRetentionExpiry);
    }

    [Fact]
    public async Task CommerceExpireAsync_SkipsTheCommerceContributorsMainNeverRanOnExpiry()
    {
        // Arrange
        var expiring = new Mock<ICommercePersonalDataContributor>();
        expiring.Setup(contributor => contributor.TakesPartInRetentionExpiry).Returns(true);
        var optedOut = new Mock<ICommercePersonalDataContributor>();
        optedOut.Setup(contributor => contributor.TakesPartInRetentionExpiry).Returns(false);

        var service = new DefaultCommercePersonalDataService([expiring.Object, optedOut.Object], Clock());

        // Act
        await service.ExpireAsync(null, "ann@example.com", TestContext.Current.CancellationToken);

        // Assert
        expiring.Verify(contributor => contributor.AnonymizeAsync(It.IsAny<CommercePersonalDataAnonymizeContext>(), It.IsAny<CancellationToken>()), Times.Once);
        optedOut.Verify(contributor => contributor.AnonymizeAsync(It.IsAny<CommercePersonalDataAnonymizeContext>(), It.IsAny<CancellationToken>()), Times.Never);
        optedOut.Verify(contributor => contributor.IdentifyAsync(It.IsAny<CommercePersonalDataSubject>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static DefaultCustomerPrivacyService CreatePrivacyService(ISession session, IContentManager contentManager, ICommercePersonalDataService commerce)
    {
        var crm = new CrmContactPersonalDataContributor(
            new CrmPersonalDataContacts(session, new DefaultPhoneNumberService()),
            TestContactMethods.Accessor,
            contentManager,
            new PassThroughStringLocalizer<CrmContactPersonalDataContributor>(),
            []);

        var personalData = new DefaultPersonalDataService([new CommercePersonalDataContributor(commerce), crm], Clock());

        return new DefaultCustomerPrivacyService(new InMemoryCustomerStore(), [new PersonalDataCustomerPrivacyHandler(personalData)], Clock());
    }

    private static Customer Guest(string email, string phone)
        => new()
        {
            ItemId = "guest-1",
            DisplayName = "Ann Lee",
            FirstName = "Ann",
            LastName = "Lee",
            Email = email,
            Phone = phone,
            LastActivityUtc = _now.AddMonths(-30),
        };

    private static IClock Clock()
    {
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        return clock.Object;
    }

    private sealed class RecordingContributor : IPersonalDataContributor
    {
        private readonly string _name;
        private readonly List<string> _log;

        public RecordingContributor(string name, bool takesPart, List<string> log)
        {
            _name = name;
            TakesPartInRetentionExpiry = takesPart;
            _log = log;
        }

        public bool TakesPartInRetentionExpiry { get; }

        public Task IdentifyAsync(PersonalDataSubject subject, CancellationToken cancellationToken = default)
        {
            _log.Add($"identify:{_name}");

            return Task.CompletedTask;
        }

        public Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
        {
            _log.Add($"anonymize:{_name}:{context.Kind}");
            context.Result.Count(_name);

            return Task.CompletedTask;
        }
    }
}
