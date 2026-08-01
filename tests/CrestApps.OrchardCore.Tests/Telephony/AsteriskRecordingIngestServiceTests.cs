using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskRecordingIngestServiceTests
{
    private static readonly DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private const string _interactionId = "interaction-1";
    private static readonly string _recordingName = AsteriskAriConstants.RecordingNamePrefix + _interactionId;

    [Fact]
    public async Task ProcessDueAsync_WhenRecordingDownloads_StoresEncryptedAndMarksCompleted()
    {
        // Arrange
        var jobStore = new FakeAsteriskRecordingIngestJobStore();
        await jobStore.EnqueueAsync(_interactionId, _recordingName, "wav", _now, TestContext.Current.CancellationToken);

        var recordingBytes = new byte[] { 1, 2, 3, 4 };
        var ariClient = new Mock<IAsteriskAriClient>();
        ariClient
            .Setup(client => client.DownloadStoredRecordingAsync(_recordingName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AsteriskAriStoredRecordingContent { Content = recordingBytes, ContentType = "audio/wav" });

        var mediaStore = new RecordingMediaStoreSpy();
        var service = CreateService(jobStore, ariClient.Object, mediaStore);

        // Act
        var ingested = await service.ProcessDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, ingested);

        var job = await jobStore.GetByRecordingNameAsync(_recordingName, TestContext.Current.CancellationToken);

        Assert.Equal(RecordingIngestJobStatus.Completed, job.Status);
        Assert.Equal("media-ref-" + _recordingName, job.MediaReference);
        Assert.Null(job.LastError);

        var stored = Assert.Single(mediaStore.Stored);

        Assert.Equal(_recordingName, stored.StorageKey);
        Assert.Equal(_interactionId, stored.InteractionId);
        Assert.Equal(recordingBytes, stored.Content);

        ariClient.Verify(
            client => client.DeleteStoredRecordingAsync(_recordingName, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessDueAsync_WhenSourceCleanupFails_RetriesCleanupWithoutReStoring()
    {
        // Arrange
        var jobStore = new FakeAsteriskRecordingIngestJobStore();
        await jobStore.EnqueueAsync(_interactionId, _recordingName, "wav", _now, TestContext.Current.CancellationToken);

        var ariClient = new Mock<IAsteriskAriClient>();
        ariClient
            .Setup(client => client.DownloadStoredRecordingAsync(_recordingName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AsteriskAriStoredRecordingContent { Content = [1, 2, 3] });
        ariClient
            .SetupSequence(client => client.DeleteStoredRecordingAsync(_recordingName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AsteriskAriException("The source could not be deleted."))
            .Returns(Task.CompletedTask);

        var mediaStore = new RecordingMediaStoreSpy();

        // Act — first pass stores the encrypted copy but fails to clean up the plaintext source.
        await CreateService(jobStore, ariClient.Object, mediaStore, _now).ProcessDueAsync(TestContext.Current.CancellationToken);

        var afterFirst = await jobStore.GetByRecordingNameAsync(_recordingName, TestContext.Current.CancellationToken);
        var statusAfterFirst = afterFirst.Status;
        var mediaStoredAfterFirst = afterFirst.MediaStored;

        // Act — a later pass (past the back-off) retries only the cleanup.
        await CreateService(jobStore, ariClient.Object, mediaStore, _now.AddMinutes(5)).ProcessDueAsync(TestContext.Current.CancellationToken);

        var afterSecond = await jobStore.GetByRecordingNameAsync(_recordingName, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(RecordingIngestJobStatus.Pending, statusAfterFirst);
        Assert.True(mediaStoredAfterFirst);
        Assert.Equal(RecordingIngestJobStatus.Completed, afterSecond.Status);
        Assert.Single(mediaStore.Stored);

        ariClient.Verify(
            client => client.DownloadStoredRecordingAsync(_recordingName, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessDueAsync_WhenRecordingNotYetAvailable_RetriesWithBackoff()
    {
        // Arrange
        var jobStore = new FakeAsteriskRecordingIngestJobStore();
        await jobStore.EnqueueAsync(_interactionId, _recordingName, "wav", _now, TestContext.Current.CancellationToken);

        var ariClient = new Mock<IAsteriskAriClient>();
        ariClient
            .Setup(client => client.DownloadStoredRecordingAsync(_recordingName, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AsteriskAriStoredRecordingContent)null);

        var mediaStore = new RecordingMediaStoreSpy();
        var service = CreateService(jobStore, ariClient.Object, mediaStore);

        // Act
        var ingested = await service.ProcessDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, ingested);

        var job = await jobStore.GetByRecordingNameAsync(_recordingName, TestContext.Current.CancellationToken);

        Assert.Equal(RecordingIngestJobStatus.Pending, job.Status);
        Assert.Equal(1, job.AttemptCount);
        Assert.Equal(_now.AddSeconds(AsteriskAriConstants.RecordingIngestBaseBackoffSeconds), job.NextAttemptUtc);
        Assert.NotNull(job.LastError);
        Assert.Empty(mediaStore.Stored);
    }

    [Fact]
    public async Task ProcessDueAsync_WhenDownloadThrows_RecordsFailureWithoutStopping()
    {
        // Arrange
        var jobStore = new FakeAsteriskRecordingIngestJobStore();
        await jobStore.EnqueueAsync(_interactionId, _recordingName, "wav", _now, TestContext.Current.CancellationToken);

        var ariClient = new Mock<IAsteriskAriClient>();
        ariClient
            .Setup(client => client.DownloadStoredRecordingAsync(_recordingName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AsteriskAriException("The download failed."));

        var mediaStore = new RecordingMediaStoreSpy();
        var service = CreateService(jobStore, ariClient.Object, mediaStore);

        // Act
        var ingested = await service.ProcessDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, ingested);

        var job = await jobStore.GetByRecordingNameAsync(_recordingName, TestContext.Current.CancellationToken);

        Assert.Equal(RecordingIngestJobStatus.Pending, job.Status);
        Assert.Equal(1, job.AttemptCount);
        Assert.NotNull(job.LastError);
    }

    [Fact]
    public async Task ProcessDueAsync_WhenAttemptBudgetExhausted_DeadLetters()
    {
        // Arrange
        var jobStore = new FakeAsteriskRecordingIngestJobStore();
        await jobStore.EnqueueAsync(_interactionId, _recordingName, "wav", _now, TestContext.Current.CancellationToken);

        var seeded = await jobStore.GetByRecordingNameAsync(_recordingName, TestContext.Current.CancellationToken);
        seeded.AttemptCount = AsteriskAriConstants.RecordingIngestMaxAttempts - 1;
        await jobStore.UpdateAsync(seeded, TestContext.Current.CancellationToken);

        var ariClient = new Mock<IAsteriskAriClient>();
        ariClient
            .Setup(client => client.DownloadStoredRecordingAsync(_recordingName, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AsteriskAriStoredRecordingContent)null);

        var service = CreateService(jobStore, ariClient.Object, new RecordingMediaStoreSpy());

        // Act
        await service.ProcessDueAsync(TestContext.Current.CancellationToken);

        // Assert
        var job = await jobStore.GetByRecordingNameAsync(_recordingName, TestContext.Current.CancellationToken);

        Assert.Equal(RecordingIngestJobStatus.DeadLettered, job.Status);
        Assert.Equal(AsteriskAriConstants.RecordingIngestMaxAttempts, job.AttemptCount);
    }

    [Fact]
    public async Task ProcessDueAsync_WhenOneJobPoisons_OtherJobsStillIngest()
    {
        // Arrange
        var jobStore = new FakeAsteriskRecordingIngestJobStore();
        var poisonRecording = AsteriskAriConstants.RecordingNamePrefix + "interaction-poison";
        var healthyRecording = AsteriskAriConstants.RecordingNamePrefix + "interaction-healthy";

        await jobStore.EnqueueAsync("interaction-poison", poisonRecording, "wav", _now, TestContext.Current.CancellationToken);
        await jobStore.EnqueueAsync("interaction-healthy", healthyRecording, "wav", _now, TestContext.Current.CancellationToken);

        var ariClient = new Mock<IAsteriskAriClient>();
        ariClient
            .Setup(client => client.DownloadStoredRecordingAsync(poisonRecording, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Boom."));
        ariClient
            .Setup(client => client.DownloadStoredRecordingAsync(healthyRecording, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AsteriskAriStoredRecordingContent { Content = new byte[] { 9, 9 } });

        var mediaStore = new RecordingMediaStoreSpy();
        var service = CreateService(jobStore, ariClient.Object, mediaStore);

        // Act
        var ingested = await service.ProcessDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, ingested);

        var poisonJob = await jobStore.GetByRecordingNameAsync(poisonRecording, TestContext.Current.CancellationToken);
        var healthyJob = await jobStore.GetByRecordingNameAsync(healthyRecording, TestContext.Current.CancellationToken);

        Assert.Equal(RecordingIngestJobStatus.Pending, poisonJob.Status);
        Assert.Equal(1, poisonJob.AttemptCount);
        Assert.Equal(RecordingIngestJobStatus.Completed, healthyJob.Status);
    }

    [Fact]
    public async Task ProcessDueAsync_WhenRecordingErasedBeforeStore_CancelsWithoutStoringMedia()
    {
        // Arrange
        var jobStore = new FakeAsteriskRecordingIngestJobStore();
        await jobStore.EnqueueAsync(_interactionId, _recordingName, "wav", _now, TestContext.Current.CancellationToken);

        var ariClient = new Mock<IAsteriskAriClient>();
        var mediaStore = new RecordingMediaStoreSpy();
        var erasureGuard = new Mock<IRecordingErasureGuard>();
        erasureGuard
            .Setup(guard => guard.IsRecordingErasedAsync(_interactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService(jobStore, ariClient.Object, mediaStore, erasureGuard.Object);

        // Act
        var ingested = await service.ProcessDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, ingested);

        var job = await jobStore.GetByRecordingNameAsync(_recordingName, TestContext.Current.CancellationToken);

        Assert.Equal(RecordingIngestJobStatus.Cancelled, job.Status);
        Assert.Empty(mediaStore.Stored);

        // Cancellation defensively deletes by the deterministic storage key even though this pass stored nothing, so
        // media that a prior attempt stored before crashing (leaving MediaStored/MediaReference unpersisted) is still
        // cleaned up rather than orphaned. Deletion is idempotent, so an absent object is a no-op.
        Assert.Equal([_recordingName], mediaStore.Deleted);

        ariClient.Verify(
            client => client.DownloadStoredRecordingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        ariClient.Verify(
            client => client.DeleteStoredRecordingAsync(_recordingName, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessDueAsync_WhenRecordingErasedDuringStore_DeletesStoredMediaAndCancels()
    {
        // Arrange
        var jobStore = new FakeAsteriskRecordingIngestJobStore();
        await jobStore.EnqueueAsync(_interactionId, _recordingName, "wav", _now, TestContext.Current.CancellationToken);

        var recordingBytes = new byte[] { 1, 2, 3, 4 };
        var ariClient = new Mock<IAsteriskAriClient>();
        ariClient
            .Setup(client => client.DownloadStoredRecordingAsync(_recordingName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AsteriskAriStoredRecordingContent { Content = recordingBytes, ContentType = "audio/wav" });

        var mediaStore = new RecordingMediaStoreSpy();
        var erasureGuard = new Mock<IRecordingErasureGuard>();

        // Not erased on the pre-store check, then erased on the post-store re-check: an erasure landed during the
        // download/store window and the just-written media must be deleted.
        erasureGuard
            .SetupSequence(guard => guard.IsRecordingErasedAsync(_interactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true);

        var service = CreateService(jobStore, ariClient.Object, mediaStore, erasureGuard.Object);

        // Act
        var ingested = await service.ProcessDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, ingested);

        var job = await jobStore.GetByRecordingNameAsync(_recordingName, TestContext.Current.CancellationToken);

        Assert.Equal(RecordingIngestJobStatus.Cancelled, job.Status);
        Assert.Single(mediaStore.Stored);
        Assert.Equal(["media-ref-" + _recordingName], mediaStore.Deleted);

        ariClient.Verify(
            client => client.DeleteStoredRecordingAsync(_recordingName, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static AsteriskRecordingIngestService CreateService(
        IAsteriskRecordingIngestJobStore jobStore,
        IAsteriskAriClient ariClient,
        IRecordingMediaStore mediaStore,
        IRecordingErasureGuard erasureGuard = null)
    {
        return CreateService(jobStore, ariClient, mediaStore, _now, erasureGuard);
    }

    private static AsteriskRecordingIngestService CreateService(
        IAsteriskRecordingIngestJobStore jobStore,
        IAsteriskAriClient ariClient,
        IRecordingMediaStore mediaStore,
        DateTime nowUtc,
        IRecordingErasureGuard erasureGuard = null)
    {
        return new AsteriskRecordingIngestService(
            jobStore,
            ariClient,
            mediaStore,
            erasureGuard is null ? [] : [erasureGuard],
            new StubClock(nowUtc),
            NullLogger<AsteriskRecordingIngestService>.Instance);
    }

    private sealed class RecordingMediaStoreSpy : IRecordingMediaStore
    {
        public List<RecordingMediaWriteRequest> Stored { get; } = [];

        public List<string> Deleted { get; } = [];

        public Task<string> StoreAsync(RecordingMediaWriteRequest request, CancellationToken cancellationToken = default)
        {
            Stored.Add(request);

            return Task.FromResult("media-ref-" + request.StorageKey);
        }

        public Task<Stream> OpenReadAsync(string storageReference, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Stream>(null);
        }

        public Task<bool> DeleteAsync(string storageReference, CancellationToken cancellationToken = default)
        {
            Deleted.Add(storageReference);

            return Task.FromResult(true);
        }
    }
}
