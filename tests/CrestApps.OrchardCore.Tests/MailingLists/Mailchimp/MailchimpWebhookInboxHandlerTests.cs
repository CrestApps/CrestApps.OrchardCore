using System.Text.Json;
using CrestApps.OrchardCore.MailingLists.Mailchimp;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Models;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Services;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.MailingLists.Mailchimp;

public sealed class MailchimpWebhookInboxHandlerTests
{
    private const string UnsubscribeBody = "type=unsubscribe&fired_at=2026-11-22+10%3A00%3A00&data%5Baction%5D=unsub&data%5Breason%5D=manual&data%5Blist_id%5D=aud123&data%5Bemail%5D=a%40example.com";

    private static readonly DateTime _receivedUtc = new(2026, 11, 22, 10, 0, 5, DateTimeKind.Utc);

    [Fact]
    public void Handler_IsNamed_AndNaturallyIdempotent()
    {
        // Arrange
        var handler = CreateHandler(MailchimpTestContext.List(), new RecordingMemberService());

        // Act and assert
        Assert.Equal(MailchimpConstants.WebhookHandlerName, handler.TechnicalName);
        Assert.Equal(WebhookHandlerReplaySafety.NaturallyIdempotent, handler.ReplaySafety);
    }

    [Fact]
    public async Task HandleAsync_AppliesTheWebhookThroughTheProviderChangePath()
    {
        // Arrange
        var members = new RecordingMemberService();
        members.Add("a@example.com", MailingListMemberStatus.Subscribed, new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        var handler = CreateHandler(MailchimpTestContext.List(), members);

        // Act
        await handler.HandleAsync(Payload(UnsubscribeBody), TestContext.Current.CancellationToken);

        // Assert
        var (change, changed) = Assert.Single(members.Applied);
        Assert.True(changed);
        Assert.Equal("list-1", change.ListId);
        Assert.Equal("a@example.com", change.Email);
        Assert.Equal(MailingListMemberStatus.Unsubscribed, change.Status);
        Assert.Equal("manual", change.Reason);
        Assert.Equal(new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc), change.OccurredUtc);
        Assert.Equal(MailingListMemberStatus.Unsubscribed, members.StatusOf("a@example.com"));
    }

    [Fact]
    public async Task HandleAsync_TheSameWebhookDeliveredTwice_AppliesTheSameChange_AndTheSecondChangesNothing()
    {
        // Arrange
        // The inbox delivers at least once: a redelivery replays the stored payload, so the change carries the same
        // fired_at and the member service's ordering guard leaves the member as the first delivery left it.
        var members = new RecordingMemberService();
        members.Add("a@example.com", MailingListMemberStatus.Subscribed, new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        var handler = CreateHandler(MailchimpTestContext.List(), members);
        var payload = Payload(UnsubscribeBody);

        // Act
        await handler.HandleAsync(payload, TestContext.Current.CancellationToken);
        await handler.HandleAsync(payload, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, members.Applied.Count);
        Assert.Equal(members.Applied[0].Change.OccurredUtc, members.Applied[1].Change.OccurredUtc);
        Assert.True(members.Applied[0].Changed);
        Assert.False(members.Applied[1].Changed);
        Assert.Equal(MailingListMemberStatus.Unsubscribed, members.StatusOf("a@example.com"));
    }

    [Fact]
    public async Task HandleAsync_ALateSubscribe_AfterALaterUnsubscribe_NeverSubscribesAgain()
    {
        // Arrange
        var members = new RecordingMemberService();
        members.Add("a@example.com", MailingListMemberStatus.Unsubscribed, new DateTime(2026, 11, 22, 11, 0, 0, DateTimeKind.Utc));
        var handler = CreateHandler(MailchimpTestContext.List(), members);
        const string body = "type=subscribe&fired_at=2026-11-22+10%3A00%3A00&data%5Blist_id%5D=aud123&data%5Bemail%5D=a%40example.com";

        // Act
        await handler.HandleAsync(Payload(body), TestContext.Current.CancellationToken);

        // Assert
        var (change, changed) = Assert.Single(members.Applied);
        Assert.Equal(MailingListMemberStatus.Subscribed, change.Status);
        Assert.True(change.Verified);
        Assert.False(changed);
        Assert.Equal(MailingListMemberStatus.Unsubscribed, members.StatusOf("a@example.com"));
    }

    [Fact]
    public async Task HandleAsync_AWebhookAboutAnotherAudience_IsIgnored()
    {
        // Arrange
        var members = new RecordingMemberService();
        var handler = CreateHandler(MailchimpTestContext.List(), members);

        // Act
        await handler.HandleAsync(Payload(UnsubscribeBody.Replace("aud123", "other9", StringComparison.Ordinal)), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(members.Applied);
    }

    [Fact]
    public async Task HandleAsync_AListThatIsGoneOrNoLongerInMailchimp_IsIgnored()
    {
        // Arrange
        var members = new RecordingMemberService();
        var local = MailchimpTestContext.List();
        local.ProviderName = "Local";

        // Act
        await CreateHandler(null, members).HandleAsync(Payload(UnsubscribeBody), TestContext.Current.CancellationToken);
        await CreateHandler(local, members).HandleAsync(Payload(UnsubscribeBody), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(members.Applied);
    }

    [Fact]
    public async Task HandleAsync_ACampaignEvent_ChangesNoMember()
    {
        // Arrange
        var members = new RecordingMemberService();
        var handler = CreateHandler(MailchimpTestContext.List(), members);

        // Act
        await handler.HandleAsync(Payload("type=campaign&fired_at=2026-11-22+10%3A00%3A00&data%5Bid%5D=5aa2102003&data%5Bstatus%5D=sent&data%5Blist_id%5D=aud123"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(members.Applied);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"Body":"type=subscribe"}""")]
    public async Task HandleAsync_APayloadThatCannotBeRead_Throws_SoTheInboxKeepsIt(string payload)
    {
        // Arrange
        var handler = CreateHandler(MailchimpTestContext.List(), new RecordingMemberService());

        // Act and assert
        await Assert.ThrowsAnyAsync<Exception>(() => handler.HandleAsync(payload, TestContext.Current.CancellationToken));
    }

    private static string Payload(string body)
        => JsonSerializer.Serialize(
            new MailchimpWebhookPayload
            {
                ListId = "list-1",
                Body = body,
                ReceivedUtc = _receivedUtc,
            },
            JsonSerializerOptions.Web);

    private static MailchimpWebhookInboxHandler CreateHandler(MailingList list, RecordingMemberService members)
    {
        var lists = new Mock<IMailingListManager>();
        lists
            .Setup(manager => manager.FindByIdAsync("list-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);

        return new MailchimpWebhookInboxHandler(lists.Object, members.Object, NullLogger<MailchimpWebhookInboxHandler>.Instance);
    }

    /// <summary>
    /// Keeps members by address and applies provider changes with the contract's ordering rule: a change older than
    /// the member's last status change, or one that moves nothing, changes nothing.
    /// </summary>
    private sealed class RecordingMemberService
    {
        private readonly Dictionary<string, MailingListMember> _members = new(StringComparer.OrdinalIgnoreCase);

        public RecordingMemberService()
        {
            var service = new Mock<IMailingListMemberService>(MockBehavior.Strict);
            service
                .Setup(members => members.ApplyProviderChangeAsync(It.IsAny<MailingListProviderChange>(), It.IsAny<CancellationToken>()))
                .Returns<MailingListProviderChange, CancellationToken>((change, _) => Task.FromResult(Apply(change)));

            Object = service.Object;
        }

        public IMailingListMemberService Object { get; }

        public List<(MailingListProviderChange Change, bool Changed)> Applied { get; } = [];

        public void Add(string email, MailingListMemberStatus status, DateTime statusChangedUtc)
            => _members[email] = new MailingListMember
            {
                NormalizedEmail = email,
                Status = status,
                StatusChangedUtc = statusChangedUtc,
            };

        public MailingListMemberStatus StatusOf(string email)
            => _members[email].Status;

        private bool Apply(MailingListProviderChange change)
        {
            var changed = false;

            if (_members.TryGetValue(change.Email, out var member)
                && change.OccurredUtc >= member.StatusChangedUtc
                && change.Status is { } status
                && status != member.Status)
            {
                member.Status = status;
                member.StatusChangedUtc = change.OccurredUtc;
                changed = true;
            }

            Applied.Add((change, changed));

            return changed;
        }
    }
}
