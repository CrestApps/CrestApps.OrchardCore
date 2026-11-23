using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.MailingLists.Core;
using CrestApps.OrchardCore.MailingLists.Core.Services;
using CrestApps.OrchardCore.MailingLists.Core.Sync;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.MailingLists;

/// <summary>
/// Builds the member service on in-memory members, a recording e-mail sender and consent the test sets.
/// </summary>
internal sealed class MailingListServiceHarness
{
    private readonly Dictionary<string, MailingList> _lists = new(StringComparer.Ordinal);

    public MailingListServiceHarness(params IMailingListMemberObserver[] observers)
    {
        // The data protection keys check a token's end against the real time, so the test clock starts at it.
        Clock = new MailingListTestClock { UtcNow = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc) };
        Tokens = SignedLinkDoubles.Tokens(clock: Clock);

        var lists = new Mock<IMailingListManager>();

        lists.Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => id is not null && _lists.TryGetValue(id, out var list) ? list : null);

        Consent.Setup(service => service.GetAsync(It.IsAny<ConsentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConsentQuery query, CancellationToken _) => query.Purpose == ConsentPurposes.Marketing && MarketingOptOut
                ? ConsentStatus.OptedOutBy("MarketingUnsubscribe")
                : string.IsNullOrEmpty(query.Purpose) && DoNotEmail
                    ? ConsentStatus.OptedOutBy("ContactPreferences")
                    : ConsentStatus.NotOptedOut);

        Consent.Setup(service => service.RecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ConsentChange>(), It.IsAny<CancellationToken>()))
            .Callback((string channel, string address, ConsentChange change, CancellationToken _) => Recorded.Add(change))
            .Returns(Task.CompletedTask);

        Sync.Setup(queue => queue.QueueAsync(It.IsAny<MailingList>(), It.IsAny<MailingListMember>(), It.IsAny<MailingListPushKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var throttle = new MailingListConfirmationThrottle(cache, Options.Create(new MailingListsOptions()));

        Service = new DefaultMailingListMemberService(
            lists.Object,
            Members,
            Mock.Of<IMailingListProviderResolver>(),
            Sync.Object,
            Emails,
            throttle,
            new MailingListMemberObserverRunner(observers, NullLogger<MailingListMemberObserverRunner>.Instance),
            Consent.Object,
            new KeyedDistributedLock(),
            Tokens,
            Clock,
            NullLogger<DefaultMailingListMemberService>.Instance);
    }

    public MailingListTestClock Clock { get; }

    public SignedTokenService Tokens { get; }

    public InMemoryMailingListMemberStore Members { get; } = new();

    public RecordingMailingListEmails Emails { get; } = new();

    public Mock<IConsentService> Consent { get; } = new();

    public Mock<IMailingListSyncQueue> Sync { get; } = new();

    public List<ConsentChange> Recorded { get; } = [];

    public bool MarketingOptOut { get; set; }

    public bool DoNotEmail { get; set; }

    public DefaultMailingListMemberService Service { get; }

    public MailingList AddList(MailingListConfirmation confirmation, bool sendWelcome = true)
    {
        var list = new MailingList
        {
            ItemId = "list" + (_lists.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(22, '0'),
            Name = "news" + _lists.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DisplayText = "News",
            Confirmation = confirmation,
            SendWelcomeEmail = sendWelcome,
            ConsentText = "Send me the news.",
        };

        _lists[list.ItemId] = list;

        return list;
    }

    public static MailingListSubscribeRequest SignUp(MailingList list, string email = "Ada@Example.com")
        => new()
        {
            ListId = list.ItemId,
            Email = email,
            Source = MailingListMemberSources.Widget,
            SourceId = "widget1",
            ClientAddress = "203.0.113.5",
        };

    public string LinkFor(MailingListMember member, MailingList list)
        => MailingListVerificationTokens.Create(Tokens, member.ItemId, member.VerificationNonce, Clock.UtcNow, list.ConfirmationLinkLifetime);
}
