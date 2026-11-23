using CrestApps.OrchardCore.MailingLists;
using CrestApps.OrchardCore.MailingLists.Core;
using CrestApps.OrchardCore.MailingLists.Core.Services;
using CrestApps.OrchardCore.MailingLists.Core.Sync;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.WorkQueues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.MailingLists;

/// <summary>
/// The provider outbox: only a list kept on the site is synced without a push; a change for a provider whose feature is
/// off, or that is not set up, waits without using up an attempt and the member says why; a provider that can take the
/// change gets it and the member is synced.
/// </summary>
public sealed class MailingListSyncTests
{
    private const string ProviderName = "Mailchimp";

    [Fact]
    public async Task QueueAsync_AListKeptOnTheSite_QueuesNothing_AndTheMemberIsSynced()
    {
        // Arrange
        var queue = new Mock<ILeasedWorkQueue<MailingListSyncItem>>();
        var sync = new MailingListSyncQueue(new DefaultMailingListProviderResolver([]), queue.Object, new MailingListTestClock());
        var member = Member(MailingListSyncState.Failed);

        // Act
        var queued = await sync.QueueAsync(NewList(MailingListsConstants.LocalProviderName), member, MailingListPushKind.Upsert, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(queued);
        Assert.Equal(MailingListSyncState.Synced, member.SyncState);
        Assert.Null(member.SyncError);
        queue.Verify(work => work.EnqueueAsync(It.IsAny<MailingListSyncItem>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task QueueAsync_AListWhoseProviderIsNotRegistered_QueuesTheChange_AndSaysWhyItWaits()
    {
        // Arrange
        var queue = new Mock<ILeasedWorkQueue<MailingListSyncItem>>();
        var sync = new MailingListSyncQueue(new DefaultMailingListProviderResolver([]), queue.Object, new MailingListTestClock());
        var member = Member(MailingListSyncState.Synced);

        // Act
        var queued = await sync.QueueAsync(NewList(ProviderName), member, MailingListPushKind.Upsert, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(queued);
        Assert.Equal(MailingListSyncState.Pending, member.SyncState);
        Assert.Equal(MailingListSyncWaits.ProviderMissing(ProviderName), member.SyncError);
        queue.Verify(work => work.EnqueueAsync(It.Is<MailingListSyncItem>(item => item.MemberId == member.ItemId && item.Kind == MailingListPushKind.Upsert), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task QueueAsync_AListWhoseProviderIsRegistered_QueuesTheChange()
    {
        // Arrange
        var queue = new Mock<ILeasedWorkQueue<MailingListSyncItem>>();
        var sync = new MailingListSyncQueue(new DefaultMailingListProviderResolver([Provider(configured: true).Object]), queue.Object, new MailingListTestClock());
        var member = Member(MailingListSyncState.Synced);

        // Act
        var queued = await sync.QueueAsync(NewList(ProviderName), member, MailingListPushKind.Unsubscribe, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(queued);
        Assert.Equal(MailingListSyncState.Pending, member.SyncState);
        Assert.Null(member.SyncError);
        queue.Verify(work => work.EnqueueAsync(It.IsAny<MailingListSyncItem>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task RunBatchAsync_AProviderWhoseFeatureIsOff_KeepsTheChangeWaiting_WithoutUsingAnAttempt()
    {
        // Arrange
        var fixture = new ProcessorFixture();
        var member = await fixture.ClaimAsync(MailingListPushKind.Upsert);

        // Act
        var result = await fixture.Processor.RunBatchAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, result.Waiting);
        Assert.Equal(0, result.Abandoned);
        Assert.Equal(0, result.Retried);
        fixture.VerifyWaited(Times.Once());
        fixture.Queue.Verify(work => work.AbandonAsync(It.IsAny<MailingListSyncItem>(), It.IsAny<string>(), It.IsAny<Action<MailingListSyncItem>>(), It.IsAny<CancellationToken>()), Times.Never());
        fixture.Queue.Verify(work => work.CompleteAsync(It.IsAny<MailingListSyncItem>(), It.IsAny<Action<MailingListSyncItem>>(), It.IsAny<CancellationToken>()), Times.Never());
        Assert.Equal(MailingListSyncState.Pending, member.SyncState);
        Assert.Equal(MailingListSyncWaits.ProviderMissing(ProviderName), member.SyncError);
    }

    [Fact]
    public async Task RunBatchAsync_AProviderThatIsNotSetUp_KeepsTheChangeWaiting_WithoutUsingAnAttempt()
    {
        // Arrange
        var provider = Provider(configured: false);
        var fixture = new ProcessorFixture(provider.Object);
        var member = await fixture.ClaimAsync(MailingListPushKind.Upsert);

        // Act
        var result = await fixture.Processor.RunBatchAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, result.Waiting);
        fixture.VerifyWaited(Times.Once());
        provider.Verify(source => source.PushAsync(It.IsAny<MailingList>(), It.IsAny<MailingListMember>(), It.IsAny<MailingListPushKind>(), It.IsAny<CancellationToken>()), Times.Never());
        Assert.Equal(MailingListSyncState.Pending, member.SyncState);
        Assert.Equal(MailingListSyncWaits.ProviderNotConfigured(ProviderName), member.SyncError);
    }

    [Fact]
    public async Task RunBatchAsync_AnErasureForAProviderWhoseFeatureIsOff_Waits()
    {
        // Arrange
        var fixture = new ProcessorFixture();

        await fixture.ClaimAsync(MailingListPushKind.Erase);

        // Act
        var result = await fixture.Processor.RunBatchAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, result.Waiting);
        fixture.VerifyWaited(Times.Once());
    }

    [Fact]
    public async Task RunBatchAsync_AProviderThatIsBack_GetsTheChange_AndTheMemberIsSynced()
    {
        // Arrange
        var provider = Provider(configured: true);
        var fixture = new ProcessorFixture(provider.Object);
        var member = await fixture.ClaimAsync(MailingListPushKind.Upsert);

        member.SyncError = MailingListSyncWaits.ProviderMissing(ProviderName);

        // Act
        var result = await fixture.Processor.RunBatchAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, result.Sent);
        provider.Verify(source => source.PushAsync(fixture.List, member, MailingListPushKind.Upsert, It.IsAny<CancellationToken>()), Times.Once());
        fixture.Queue.Verify(work => work.CompleteAsync(It.IsAny<MailingListSyncItem>(), It.IsAny<Action<MailingListSyncItem>>(), It.IsAny<CancellationToken>()), Times.Once());
        Assert.Equal(MailingListSyncState.Synced, member.SyncState);
        Assert.Null(member.SyncError);
        Assert.Equal("mc-1", member.ProviderMemberId);
    }

    [Fact]
    public async Task RunBatchAsync_AListMovedToTheSite_CompletesTheChange_WithoutAPush()
    {
        // Arrange
        var provider = Provider(configured: true);
        var fixture = new ProcessorFixture(provider.Object);

        await fixture.ClaimAsync(MailingListPushKind.Upsert);
        fixture.List.ProviderName = MailingListsConstants.LocalProviderName;

        // Act
        var result = await fixture.Processor.RunBatchAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, result.Sent);
        provider.Verify(source => source.PushAsync(It.IsAny<MailingList>(), It.IsAny<MailingListMember>(), It.IsAny<MailingListPushKind>(), It.IsAny<CancellationToken>()), Times.Never());
        fixture.Queue.Verify(work => work.CompleteAsync(It.IsAny<MailingListSyncItem>(), It.IsAny<Action<MailingListSyncItem>>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    private static MailingList NewList(string providerName)
        => new()
        {
            ItemId = "list1",
            Name = "news",
            DisplayText = "News",
            ProviderName = providerName,
        };

    private static MailingListMember Member(MailingListSyncState state)
        => new()
        {
            ItemId = "member1",
            ListId = "list1",
            NormalizedEmail = "ada@example.com",
            Status = MailingListMemberStatus.Subscribed,
            SyncState = state,
        };

    private static Mock<IMailingListProvider> Provider(bool configured)
    {
        var provider = new Mock<IMailingListProvider>();

        provider.SetupGet(source => source.Name).Returns(ProviderName);
        provider.SetupGet(source => source.Capabilities).Returns(MailingListProviderCapabilities.ReportsChanges);
        provider.Setup(source => source.IsConfiguredAsync(It.IsAny<CancellationToken>())).ReturnsAsync(configured);
        provider.Setup(source => source.PushAsync(It.IsAny<MailingList>(), It.IsAny<MailingListMember>(), It.IsAny<MailingListPushKind>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MailingListPushResult.Success("mc-1"));

        return provider;
    }

    private sealed class ProcessorFixture
    {
        private readonly List<MailingListSyncItem> _claimed = [];

        public ProcessorFixture(params IMailingListProvider[] providers)
        {
            var lists = new Mock<IMailingListManager>();

            lists.Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => id == List.ItemId ? List : null);

            Queue.Setup(work => work.ClaimDueAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_claimed);
            Queue.Setup(work => work.CompleteAsync(It.IsAny<MailingListSyncItem>(), It.IsAny<Action<MailingListSyncItem>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            Queue.Setup(work => work.RetryAsync(It.IsAny<MailingListSyncItem>(), It.IsAny<string>(), It.IsAny<Func<int, TimeSpan>>(), It.IsAny<bool>(), It.IsAny<Action<MailingListSyncItem>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((LeasedWorkItemStatus?)LeasedWorkItemStatus.Pending);
            Queue.Setup(work => work.AbandonAsync(It.IsAny<MailingListSyncItem>(), It.IsAny<string>(), It.IsAny<Action<MailingListSyncItem>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var services = new ServiceCollection()
                .AddSingleton(lists.Object)
                .AddSingleton<IMailingListProviderResolver>(new DefaultMailingListProviderResolver(providers))
                .AddSingleton<IMailingListMemberStore>(Members)
                .BuildServiceProvider();

            Processor = new MailingListSyncProcessor(
                Queue.Object,
                services.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(new MailingListsOptions()),
                NullLogger<MailingListSyncProcessor>.Instance);
        }

        public MailingList List { get; } = NewList(ProviderName);

        public InMemoryMailingListMemberStore Members { get; } = new();

        public Mock<ILeasedWorkQueue<MailingListSyncItem>> Queue { get; } = new();

        public MailingListSyncProcessor Processor { get; }

        public async Task<MailingListMember> ClaimAsync(MailingListPushKind kind)
        {
            var member = Member(MailingListSyncState.Pending);

            await Members.CreateAsync(member, TestContext.Current.CancellationToken);
            _claimed.Add(new MailingListSyncItem
            {
                ItemId = "item" + _claimed.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ListId = List.ItemId,
                MemberId = member.ItemId,
                Kind = kind,
                Email = kind == MailingListPushKind.Erase ? member.NormalizedEmail : null,
                OwnerToken = "owner",
            });

            return member;
        }

        public void VerifyWaited(Times times)
        {
            Queue.Verify(work => work.RetryAsync(It.IsAny<MailingListSyncItem>(), It.IsAny<string>(), It.IsAny<Func<int, TimeSpan>>(), false, It.IsAny<Action<MailingListSyncItem>>(), It.IsAny<CancellationToken>()), times);
            Queue.Verify(work => work.RetryAsync(It.IsAny<MailingListSyncItem>(), It.IsAny<string>(), It.IsAny<Func<int, TimeSpan>>(), true, It.IsAny<Action<MailingListSyncItem>>(), It.IsAny<CancellationToken>()), Times.Never());
        }
    }
}
