using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.MailingLists;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.MailingLists;

/// <summary>
/// Sign-up, verification and unsubscribe through the member service: each confirmation mode, the two consent checks,
/// the same answer for an address already on the list, the verification link (expired, rotated, second click), an old
/// link after an unsubscribe, the provider's changes (silent for addresses that never signed up on the site, a push when
/// an observer asks for one) and retrying a failed sync.
/// </summary>
public sealed class DefaultMailingListMemberServiceTests
{
    [Fact]
    public async Task SubscribeAsync_ADoubleOptInList_KeepsTheMemberPending_AndSendsTheConfirmLink()
    {
        // Arrange
        var harness = new MailingListServiceHarness();
        var list = harness.AddList(MailingListConfirmation.Email);

        // Act
        var result = await harness.Service.SubscribeAsync(MailingListServiceHarness.SignUp(list), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MailingListSubscribeOutcome.ConfirmationSent, result.Outcome);
        var member = Assert.Single(harness.Members.All);
        Assert.Equal(MailingListMemberStatus.Pending, member.Status);
        Assert.Equal("ada@example.com", member.NormalizedEmail);
        Assert.Equal("Send me the news.", member.Consent.Text);
        Assert.Equal("203.0.113.5", member.Consent.ClientAddress);
        var sent = Assert.Single(harness.Emails.Sent);
        Assert.Equal("confirm", sent.Kind);
        Assert.False(string.IsNullOrEmpty(sent.Nonce));
    }

    [Fact]
    public async Task SubscribeAsync_ASingleOptInList_SubscribesAtOnce_AndSendsTheWelcome()
    {
        // Arrange
        var harness = new MailingListServiceHarness();
        var list = harness.AddList(MailingListConfirmation.None);

        // Act
        var result = await harness.Service.SubscribeAsync(MailingListServiceHarness.SignUp(list), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MailingListSubscribeOutcome.Subscribed, result.Outcome);
        Assert.Equal(MailingListMemberStatus.Subscribed, Assert.Single(harness.Members.All).Status);
        Assert.Equal("welcome", Assert.Single(harness.Emails.Sent).Kind);
        Assert.Empty(harness.Recorded);
    }

    [Fact]
    public async Task SubscribeAsync_ASingleOptInListAndAMarketingOptOutOnFile_SendsAConfirmationAnyway()
    {
        // Arrange
        var harness = new MailingListServiceHarness { MarketingOptOut = true };
        var list = harness.AddList(MailingListConfirmation.None);

        // Act
        var result = await harness.Service.SubscribeAsync(MailingListServiceHarness.SignUp(list), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MailingListSubscribeOutcome.ConfirmationSent, result.Outcome);
        Assert.Equal(MailingListMemberStatus.Pending, Assert.Single(harness.Members.All).Status);
        Assert.Equal("confirm", Assert.Single(harness.Emails.Sent).Kind);
    }

    [Fact]
    public async Task SubscribeAsync_AnAddressThatMayNotBeEmailed_IsRefused_WithoutAnEmail()
    {
        // Arrange
        var harness = new MailingListServiceHarness { DoNotEmail = true };
        var list = harness.AddList(MailingListConfirmation.Email);

        // Act
        var result = await harness.Service.SubscribeAsync(MailingListServiceHarness.SignUp(list), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MailingListSubscribeOutcome.Refused, result.Outcome);
        Assert.Empty(harness.Members.All);
        Assert.Empty(harness.Emails.Sent);
    }

    [Fact]
    public async Task SubscribeAsync_AnAddressAlreadySubscribed_ChangesNothing_AndSendsNothing()
    {
        // Arrange
        var harness = new MailingListServiceHarness();
        var list = harness.AddList(MailingListConfirmation.None);
        await harness.Service.SubscribeAsync(MailingListServiceHarness.SignUp(list), TestContext.Current.CancellationToken);
        harness.Emails.Sent.Clear();

        // Act
        var result = await harness.Service.SubscribeAsync(MailingListServiceHarness.SignUp(list, "ADA@example.com "), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MailingListSubscribeOutcome.AlreadySubscribed, result.Outcome);
        Assert.True(result.Succeeded);
        Assert.Single(harness.Members.All);
        Assert.Empty(harness.Emails.Sent);
    }

    [Fact]
    public async Task SubscribeAsync_ASignedInUserWithTheConfirmedAccountEmail_IsSubscribedAndVerified_AndOptsIn()
    {
        // Arrange
        var harness = new MailingListServiceHarness();
        var list = harness.AddList(MailingListConfirmation.Email);
        var request = new MailingListSubscribeRequest
        {
            ListId = list.ItemId,
            Email = "ada@example.com",
            Source = MailingListMemberSources.Widget,
            UserId = "user1",
            EmailVerifiedByAccount = true,
        };

        // Act
        var result = await harness.Service.SubscribeAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MailingListSubscribeOutcome.Subscribed, result.Outcome);
        var member = Assert.Single(harness.Members.All);
        Assert.NotNull(member.EmailVerifiedUtc);
        var optIn = Assert.Single(harness.Recorded);
        Assert.False(optIn.OptedOut);
        Assert.Equal(ConsentPurposes.Marketing, optIn.Purpose);
        Assert.Equal(MailingListsConstants.ConsentSourceName, optIn.Source);
    }

    [Fact]
    public async Task SubscribeAsync_AnAdminAddition_IsSilent_AndSubscribes()
    {
        // Arrange
        var harness = new MailingListServiceHarness();
        var list = harness.AddList(MailingListConfirmation.Email);
        var request = new MailingListSubscribeRequest
        {
            ListId = list.ItemId,
            Email = "ada@example.com",
            Source = MailingListMemberSources.Admin,
            Silent = true,
            Status = MailingListMemberStatus.Subscribed,
        };

        // Act
        var result = await harness.Service.SubscribeAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MailingListSubscribeOutcome.Subscribed, result.Outcome);
        Assert.Empty(harness.Emails.Sent);
    }

    [Fact]
    public async Task SubscribeAsync_MoreConfirmationsThanTheCap_IsRefused()
    {
        // Arrange
        var harness = new MailingListServiceHarness();
        var list = harness.AddList(MailingListConfirmation.Email);

        for (var i = 0; i < 3; i++)
        {
            await harness.Service.SubscribeAsync(MailingListServiceHarness.SignUp(list), TestContext.Current.CancellationToken);
        }

        // Act
        var result = await harness.Service.SubscribeAsync(MailingListServiceHarness.SignUp(list, "ada+other@example.com"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MailingListSubscribeOutcome.Refused, result.Outcome);
        Assert.Equal(3, harness.Emails.Sent.Count);
    }

    [Fact]
    public async Task VerifyAsync_TheConfirmLink_SubscribesThePendingMember_AndASecondClickChangesNothing()
    {
        // Arrange
        var harness = new MailingListServiceHarness();
        var list = harness.AddList(MailingListConfirmation.Email);
        await harness.Service.SubscribeAsync(MailingListServiceHarness.SignUp(list), TestContext.Current.CancellationToken);
        var token = harness.LinkFor(Assert.Single(harness.Members.All), list);

        // Act
        var read = await harness.Service.ReadVerificationAsync(token, TestContext.Current.CancellationToken);
        var first = await harness.Service.VerifyAsync(token, TestContext.Current.CancellationToken);
        var second = await harness.Service.VerifyAsync(token, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MailingListVerifyOutcome.Ready, read.Outcome);
        Assert.Equal(MailingListVerifyOutcome.Verified, first.Outcome);
        Assert.Equal(MailingListVerifyOutcome.AlreadyVerified, second.Outcome);
        var member = Assert.Single(harness.Members.All);
        Assert.Equal(MailingListMemberStatus.Subscribed, member.Status);
        Assert.NotNull(member.EmailVerifiedUtc);
        Assert.Single(harness.Recorded);
        Assert.Equal(new[] { "confirm", "welcome" }, harness.Emails.Sent.Select(email => email.Kind));
    }

    [Fact]
    public async Task VerifyAsync_ALinkPastItsEnd_IsExpired_AndCanAskForANewOne()
    {
        // Arrange
        var harness = new MailingListServiceHarness();
        var list = harness.AddList(MailingListConfirmation.Email);
        await harness.Service.SubscribeAsync(MailingListServiceHarness.SignUp(list), TestContext.Current.CancellationToken);
        var token = harness.LinkFor(Assert.Single(harness.Members.All), list);
        harness.Clock.Advance(list.ConfirmationLinkLifetime + TimeSpan.FromHours(1));

        // Act
        var result = await harness.Service.VerifyAsync(token, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MailingListVerifyOutcome.Expired, result.Outcome);
        Assert.NotNull(result.Member);
        Assert.Equal(MailingListMemberStatus.Pending, Assert.Single(harness.Members.All).Status);
    }

    [Fact]
    public async Task VerifyAsync_AnOlderLinkAfterANewOneWasSent_IsExpired()
    {
        // Arrange
        var harness = new MailingListServiceHarness();
        var list = harness.AddList(MailingListConfirmation.Email);
        await harness.Service.SubscribeAsync(MailingListServiceHarness.SignUp(list), TestContext.Current.CancellationToken);
        var member = Assert.Single(harness.Members.All);
        var oldToken = harness.LinkFor(member, list);

        // Act
        var resent = await harness.Service.ResendConfirmationAsync(member.ItemId, TestContext.Current.CancellationToken);
        var result = await harness.Service.VerifyAsync(oldToken, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(resent);
        Assert.Equal(MailingListVerifyOutcome.Expired, result.Outcome);
        Assert.NotEqual(harness.Emails.Sent[0].Nonce, harness.Emails.Sent[1].Nonce);
    }

    [Fact]
    public async Task UnsubscribeAsync_EndsTheMembership_AndAnOldLinkNeverBringsItBack()
    {
        // Arrange
        var harness = new MailingListServiceHarness();
        var list = harness.AddList(MailingListConfirmation.Email);
        await harness.Service.SubscribeAsync(MailingListServiceHarness.SignUp(list), TestContext.Current.CancellationToken);
        var token = harness.LinkFor(Assert.Single(harness.Members.All), list);

        // Act
        var changed = await harness.Service.UnsubscribeAsync(list.ItemId, "ADA@example.com", MailingListMemberSources.UnsubscribeLink, cancellationToken: TestContext.Current.CancellationToken);
        var result = await harness.Service.VerifyAsync(token, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, changed);
        Assert.Equal(MailingListVerifyOutcome.Expired, result.Outcome);
        var member = Assert.Single(harness.Members.All);
        Assert.Equal(MailingListMemberStatus.Unsubscribed, member.Status);
        Assert.Null(member.VerificationNonce);
    }

    [Fact]
    public async Task SubscribeAsync_AnObserverThatWaitsForVerification_GetsAClaimLinkSentInsteadOfTheWelcome()
    {
        // Arrange
        var harness = new MailingListServiceHarness(new RequestsVerificationObserver());
        var list = harness.AddList(MailingListConfirmation.None);

        // Act
        var result = await harness.Service.SubscribeAsync(MailingListServiceHarness.SignUp(list), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MailingListSubscribeOutcome.Subscribed, result.Outcome);
        var sent = Assert.Single(harness.Emails.Sent);
        Assert.Equal("claim", sent.Kind);
        Assert.False(string.IsNullOrEmpty(sent.Nonce));
    }

    [Fact]
    public async Task ApplyProviderChangeAsync_AnOlderChangeThanTheLastStatusChange_IsIgnored()
    {
        // Arrange
        var harness = new MailingListServiceHarness();
        var list = harness.AddList(MailingListConfirmation.None);
        await harness.Service.SubscribeAsync(MailingListServiceHarness.SignUp(list), TestContext.Current.CancellationToken);
        await harness.Service.UnsubscribeAsync(list.ItemId, "ada@example.com", MailingListMemberSources.UnsubscribeLink, cancellationToken: TestContext.Current.CancellationToken);
        var late = new MailingListProviderChange
        {
            ListId = list.ItemId,
            Email = "ada@example.com",
            Status = MailingListMemberStatus.Subscribed,
            OccurredUtc = harness.Clock.UtcNow.AddMinutes(-5),
        };

        // Act
        var applied = await harness.Service.ApplyProviderChangeAsync(late, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(applied);
        Assert.Equal(MailingListMemberStatus.Unsubscribed, Assert.Single(harness.Members.All).Status);
    }

    [Fact]
    public async Task ApplyProviderChangeAsync_AnAddressTheSiteNeverHad_IsSilent_AndGetsNoEmail()
    {
        // Arrange
        var observer = new RecordingObserver();
        var harness = new MailingListServiceHarness(observer);
        var list = harness.AddList(MailingListConfirmation.Provider);

        // Act
        var applied = await harness.Service.ApplyProviderChangeAsync(ProviderConfirmation(list, harness), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(applied);
        var member = Assert.Single(harness.Members.All);
        Assert.Equal(MailingListMemberSources.Provider, member.Source);
        Assert.Equal(MailingListMemberStatus.Subscribed, member.Status);
        Assert.NotEmpty(observer.Contexts);
        Assert.All(observer.Contexts, context => Assert.True(context.Silent));
        Assert.Empty(harness.Emails.Sent);
    }

    [Fact]
    public async Task ApplyProviderChangeAsync_AProviderMemberConfirmedLater_StaysSilent()
    {
        // Arrange
        var observer = new RecordingObserver();
        var harness = new MailingListServiceHarness(observer);
        var list = harness.AddList(MailingListConfirmation.Provider);
        var pending = new MailingListProviderChange
        {
            ListId = list.ItemId,
            Email = "ada@example.com",
            Status = MailingListMemberStatus.Pending,
            OccurredUtc = harness.Clock.UtcNow,
        };

        await harness.Service.ApplyProviderChangeAsync(pending, TestContext.Current.CancellationToken);
        harness.Clock.Advance(TimeSpan.FromMinutes(5));
        observer.Contexts.Clear();

        // Act
        var applied = await harness.Service.ApplyProviderChangeAsync(ProviderConfirmation(list, harness), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(applied);
        Assert.NotNull(Assert.Single(harness.Members.All).EmailVerifiedUtc);
        Assert.Contains(observer.Contexts, context => context.Event == nameof(IMailingListMemberObserver.VerifiedAsync));
        Assert.All(observer.Contexts, context => Assert.True(context.Silent));
    }

    [Fact]
    public async Task ApplyProviderChangeAsync_ASiteSignUpConfirmedByTheProvider_IsNotSilent()
    {
        // Arrange
        var observer = new RecordingObserver();
        var harness = new MailingListServiceHarness(observer);
        var list = harness.AddList(MailingListConfirmation.Provider);

        await harness.Service.SubscribeAsync(MailingListServiceHarness.SignUp(list), TestContext.Current.CancellationToken);
        harness.Clock.Advance(TimeSpan.FromMinutes(5));
        observer.Contexts.Clear();

        // Act
        var applied = await harness.Service.ApplyProviderChangeAsync(ProviderConfirmation(list, harness), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(applied);
        var verified = Assert.Single(observer.Contexts, context => context.Event == nameof(IMailingListMemberObserver.VerifiedAsync));
        Assert.False(verified.Silent);
    }

    [Fact]
    public async Task ApplyProviderChangeAsync_AnObserverThatAsksForAPush_QueuesAnUpsertForTheProvider()
    {
        // Arrange
        var harness = new MailingListServiceHarness(new RequestsProviderPushObserver());
        var list = harness.AddList(MailingListConfirmation.Provider);

        await harness.Service.SubscribeAsync(MailingListServiceHarness.SignUp(list), TestContext.Current.CancellationToken);
        harness.Clock.Advance(TimeSpan.FromMinutes(5));
        harness.Sync.Invocations.Clear();

        // Act
        await harness.Service.ApplyProviderChangeAsync(ProviderConfirmation(list, harness), TestContext.Current.CancellationToken);

        // Assert
        var member = Assert.Single(harness.Members.All);
        harness.Sync.Verify(queue => queue.QueueAsync(list, member, MailingListPushKind.Upsert, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task ApplyProviderChangeAsync_WithoutAPushRequest_PushesNothingBack()
    {
        // Arrange
        var harness = new MailingListServiceHarness();
        var list = harness.AddList(MailingListConfirmation.Provider);

        await harness.Service.SubscribeAsync(MailingListServiceHarness.SignUp(list), TestContext.Current.CancellationToken);
        harness.Clock.Advance(TimeSpan.FromMinutes(5));
        harness.Sync.Invocations.Clear();

        // Act
        await harness.Service.ApplyProviderChangeAsync(ProviderConfirmation(list, harness), TestContext.Current.CancellationToken);

        // Assert
        harness.Sync.Verify(queue => queue.QueueAsync(It.IsAny<MailingList>(), It.IsAny<MailingListMember>(), It.IsAny<MailingListPushKind>(), It.IsAny<CancellationToken>()), Times.Never());
        Assert.Equal(MailingListSyncState.Synced, Assert.Single(harness.Members.All).SyncState);
    }

    [Theory]
    [InlineData(MailingListMemberStatus.Subscribed, MailingListPushKind.Upsert)]
    [InlineData(MailingListMemberStatus.Pending, MailingListPushKind.Upsert)]
    [InlineData(MailingListMemberStatus.Unsubscribed, MailingListPushKind.Unsubscribe)]
    public async Task RetrySyncAsync_AMemberWhoseLastChangeFailed_QueuesItsStateAgain(MailingListMemberStatus status, MailingListPushKind expected)
    {
        // Arrange
        var harness = new MailingListServiceHarness();
        var list = harness.AddList(MailingListConfirmation.None);

        await harness.Service.SubscribeAsync(MailingListServiceHarness.SignUp(list), TestContext.Current.CancellationToken);

        var member = Assert.Single(harness.Members.All);

        member.Status = status;
        member.SyncState = MailingListSyncState.Failed;
        member.SyncError = "Mailchimp refused the change.";
        harness.Sync.Invocations.Clear();

        // Act
        var retried = await harness.Service.RetrySyncAsync(member.ItemId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(retried);
        harness.Sync.Verify(queue => queue.QueueAsync(list, member, expected, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task RetrySyncAsync_AMemberWhoseLastChangeDidNotFail_ChangesNothing()
    {
        // Arrange
        var harness = new MailingListServiceHarness();
        var list = harness.AddList(MailingListConfirmation.None);

        await harness.Service.SubscribeAsync(MailingListServiceHarness.SignUp(list), TestContext.Current.CancellationToken);

        var member = Assert.Single(harness.Members.All);

        harness.Sync.Invocations.Clear();

        // Act
        var retried = await harness.Service.RetrySyncAsync(member.ItemId, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(retried);
        harness.Sync.Verify(queue => queue.QueueAsync(It.IsAny<MailingList>(), It.IsAny<MailingListMember>(), It.IsAny<MailingListPushKind>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    private static MailingListProviderChange ProviderConfirmation(MailingList list, MailingListServiceHarness harness)
        => new()
        {
            ListId = list.ItemId,
            Email = "ada@example.com",
            Status = MailingListMemberStatus.Subscribed,
            Verified = true,
            OccurredUtc = harness.Clock.UtcNow,
        };

    private sealed class RecordingObserver : IMailingListMemberObserver
    {
        public List<(string Event, bool Silent)> Contexts { get; } = [];

        public Task SignedUpAsync(MailingListMemberContext context)
            => Record(nameof(SignedUpAsync), context);

        public Task SubscribedAsync(MailingListMemberContext context)
            => Record(nameof(SubscribedAsync), context);

        public Task VerifiedAsync(MailingListMemberContext context)
            => Record(nameof(VerifiedAsync), context);

        public Task UnsubscribedAsync(MailingListMemberContext context)
            => Record(nameof(UnsubscribedAsync), context);

        public Task ErasedAsync(MailingListMemberContext context)
            => Record(nameof(ErasedAsync), context);

        private Task Record(string eventName, MailingListMemberContext context)
        {
            Contexts.Add((eventName, context.Silent));

            return Task.CompletedTask;
        }
    }

    private sealed class RequestsProviderPushObserver : MailingListMemberObserverBase
    {
        public override Task VerifiedAsync(MailingListMemberContext context)
        {
            context.RequestsProviderPush = true;

            return Task.CompletedTask;
        }
    }

    private sealed class RequestsVerificationObserver : MailingListMemberObserverBase
    {
        public override Task SignedUpAsync(MailingListMemberContext context)
        {
            context.RequestsVerification = true;

            return Task.CompletedTask;
        }
    }
}
