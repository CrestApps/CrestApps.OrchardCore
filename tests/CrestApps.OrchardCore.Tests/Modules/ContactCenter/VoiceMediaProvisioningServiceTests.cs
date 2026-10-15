using System.Text;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The master copy of a clip belongs to the platform, and a provider's copy is a cache made from it. These pin
/// what that means: an upload lands in both places, a second provider is given a copy without anybody uploading
/// again, a copy a provider lost is made again, deleting an entry removes every copy, and a clip from before master
/// copies were kept still plays where it was uploaded and is reported, not silently lost, anywhere else.
/// </summary>
public sealed class VoiceMediaProvisioningServiceTests
{
    private const string Telnyx = "Telnyx";
    private const string OtherProvider = "OtherProvider";

    private static readonly byte[] _audio = Encoding.UTF8.GetBytes("RIFF....WAVEfmt ");

    [Fact]
    public async Task StoreAsync_StoresTheMasterCopy_AndGivesTheActiveProviderACopy()
    {
        // Arrange
        var mediaStore = new FakeMediaStore();
        var telnyx = new FakeProvisioner(Telnyx);
        var service = CreateService(mediaStore, [telnyx]);
        var item = new VoiceMediaItem { ItemId = "media-1", Name = "Hold music" };

        // Act
        var result = await service.StoreAsync(item, new MemoryStream(_audio), "audio/wav", "wav", "cc-voice-media", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(result.MasterStored);
        Assert.False(result.ProviderUploadFailed);
        Assert.Equal(Telnyx, result.ProviderName);
        Assert.Null(result.Replaced);

        Assert.NotNull(item.MasterStorageReference);
        Assert.Equal(_audio, mediaStore.Read(item.MasterStorageReference));
        Assert.Equal("audio/wav", item.ContentType);
        Assert.Equal("wav", item.Format);

        var upload = Assert.Single(telnyx.Uploads);
        Assert.Equal(_audio, upload.Content);
        Assert.Equal("audio/wav", upload.ContentType);
        Assert.Equal("cc-voice-media", upload.NamePrefix);
        Assert.Equal(upload.Reference, result.ProviderReference);
        Assert.Equal(upload.Reference, item.ProviderReferences[Telnyx]);
    }

    [Fact]
    public async Task StoreAsync_WhenTheProviderRefusesTheUpload_KeepsTheMasterCopyAndSaysSo()
    {
        // Arrange
        // The provider is a cache. Losing it for a moment must not lose the clip the operator just uploaded.
        var mediaStore = new FakeMediaStore();
        var telnyx = new FakeProvisioner(Telnyx) { RefuseUploads = true };
        var service = CreateService(mediaStore, [telnyx]);
        var item = new VoiceMediaItem { ItemId = "media-1" };

        // Act
        var result = await service.StoreAsync(item, new MemoryStream(_audio), "audio/mpeg", "mp3", "cc-voice-media", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.True(result.MasterStored);
        Assert.True(result.ProviderUploadFailed);
        Assert.Equal(Telnyx, result.ProviderName);
        Assert.Null(result.ProviderReference);
        Assert.NotNull(item.MasterStorageReference);
        Assert.Empty(item.ProviderReferences);
    }

    [Fact]
    public async Task StoreAsync_ReplacesEveryOldCopy_AndHandsThemBackForDeletion()
    {
        // Arrange
        // New audio makes every copy of the old audio stale, on every provider, not only the one uploaded to now.
        var mediaStore = new FakeMediaStore();
        var telnyx = new FakeProvisioner(Telnyx);
        var service = CreateService(mediaStore, [telnyx]);
        var item = new VoiceMediaItem
        {
            ItemId = "media-1",
            MasterStorageReference = "old-master",
            ProviderReferences = new Dictionary<string, string>
            {
                [Telnyx] = "old-telnyx",
                [OtherProvider] = "old-other",
            },
        };

        // Act
        var result = await service.StoreAsync(item, new MemoryStream(_audio), "audio/wav", "wav", "cc-voice-media", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result.Replaced);
        Assert.Equal("old-master", result.Replaced.MasterStorageReference);
        Assert.Equal("old-telnyx", result.Replaced.ProviderReferences[Telnyx]);
        Assert.Equal("old-other", result.Replaced.ProviderReferences[OtherProvider]);

        Assert.NotEqual("old-master", item.MasterStorageReference);
        Assert.Single(item.ProviderReferences);
        Assert.Equal(result.ProviderReference, item.ProviderReferences[Telnyx]);
    }

    [Fact]
    public async Task StoreAsync_BuffersAStreamThatCannotSeek_SoBothCopiesGetTheWholeAudio()
    {
        // Arrange
        var mediaStore = new FakeMediaStore();
        var telnyx = new FakeProvisioner(Telnyx);
        var service = CreateService(mediaStore, [telnyx]);
        var item = new VoiceMediaItem { ItemId = "media-1" };

        // Act
        await service.StoreAsync(item, new ForwardOnlyStream(_audio), "audio/wav", "wav", "cc-voice-media", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_audio, mediaStore.Read(item.MasterStorageReference));
        Assert.Equal(_audio, Assert.Single(telnyx.Uploads).Content);
    }

    [Fact]
    public async Task StoreAsync_WithNeitherAStoreNorAProvider_HoldsNothing()
    {
        // Arrange
        var service = CreateService(mediaStore: null, []);
        var item = new VoiceMediaItem { ItemId = "media-1" };

        // Act
        var result = await service.StoreAsync(item, new MemoryStream(_audio), "audio/wav", "wav", "cc-voice-media", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.False(service.CanUpload);
        Assert.Null(item.MasterStorageReference);
    }

    [Fact]
    public async Task EnsureProvisionedAsync_ForASecondProvider_UploadsFromTheMasterCopy_ThenReturnsThatCopyWithoutUploadingAgain()
    {
        // Arrange
        // The clip was uploaded while Telnyx was the provider. The tenant moves to another provider; the first call
        // that plays the clip there must find it, and the next call must not upload it a second time.
        var mediaStore = new FakeMediaStore();
        var masterReference = mediaStore.Add(_audio);
        var telnyx = new FakeProvisioner(Telnyx);
        var other = new FakeProvisioner(OtherProvider);
        var service = CreateService(mediaStore, [telnyx, other]);
        var item = new VoiceMediaItem
        {
            ItemId = "media-1",
            MasterStorageReference = masterReference,
            ContentType = "audio/wav",
            Format = "wav",
            ProviderReferences = new Dictionary<string, string> { [Telnyx] = "telnyx-copy" },
        };

        // Act
        var first = await service.EnsureProvisionedAsync(item, OtherProvider, TestContext.Current.CancellationToken);
        var second = await service.EnsureProvisionedAsync(item, OtherProvider, TestContext.Current.CancellationToken);

        // Assert
        var upload = Assert.Single(other.Uploads);
        Assert.Equal(_audio, upload.Content);
        Assert.Equal("audio/wav", upload.ContentType);
        Assert.Empty(telnyx.Uploads);

        Assert.Equal(VoiceMediaProvisioningStatus.Provisioned, first.Status);
        Assert.Equal(OtherProvider, first.ProviderName);
        Assert.Equal(upload.Reference, first.Reference);
        Assert.True(first.Changed);
        Assert.Equal(upload.Reference, item.ProviderReferences[OtherProvider]);
        Assert.Equal("telnyx-copy", item.ProviderReferences[Telnyx]);

        Assert.Equal(VoiceMediaProvisioningStatus.Provisioned, second.Status);
        Assert.Equal(upload.Reference, second.Reference);
        Assert.False(second.Changed);
        Assert.Equal(new[] { upload.Reference }, other.AvailabilityChecks);
    }

    [Fact]
    public async Task EnsureProvisionedAsync_WhenTheProviderReportsTheCopyMissing_MakesItAgainFromTheMasterCopy()
    {
        // Arrange
        // The clip was deleted at the provider, or expired there. The next caller hears it anyway.
        var mediaStore = new FakeMediaStore();
        var masterReference = mediaStore.Add(_audio);
        var telnyx = new FakeProvisioner(Telnyx) { Availability = _ => VoiceMediaAvailability.Missing };
        var service = CreateService(mediaStore, [telnyx]);
        var item = new VoiceMediaItem
        {
            ItemId = "media-1",
            MasterStorageReference = masterReference,
            ContentType = "audio/mpeg",
            ProviderReferences = new Dictionary<string, string> { [Telnyx] = "gone" },
        };

        // Act
        var result = await service.EnsureProvisionedAsync(item, Telnyx, TestContext.Current.CancellationToken);

        // Assert
        var upload = Assert.Single(telnyx.Uploads);
        Assert.Equal(_audio, upload.Content);
        Assert.Equal(VoiceMediaProvisioningStatus.Provisioned, result.Status);
        Assert.Equal(upload.Reference, result.Reference);
        Assert.True(result.Changed);
        Assert.Equal(upload.Reference, item.ProviderReferences[Telnyx]);
    }

    [Fact]
    public async Task EnsureProvisionedAsync_WhenTheProviderCannotBeAsked_KeepsTheCopyItHas()
    {
        // Arrange
        // An outage at the provider is not a reason to upload the clip again on every call.
        var mediaStore = new FakeMediaStore();
        var masterReference = mediaStore.Add(_audio);
        var telnyx = new FakeProvisioner(Telnyx) { Availability = _ => VoiceMediaAvailability.Unknown };
        var service = CreateService(mediaStore, [telnyx]);
        var item = new VoiceMediaItem
        {
            ItemId = "media-1",
            MasterStorageReference = masterReference,
            ProviderReferences = new Dictionary<string, string> { [Telnyx] = "telnyx-copy" },
        };

        // Act
        var result = await service.EnsureProvisionedAsync(item, Telnyx, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(telnyx.Uploads);
        Assert.Equal("telnyx-copy", result.Reference);
        Assert.False(result.Changed);
    }

    [Fact]
    public async Task EnsureProvisionedAsync_WhenTheProviderRefusesTheCopy_ReportsItUnavailable()
    {
        // Arrange
        var mediaStore = new FakeMediaStore();
        var masterReference = mediaStore.Add(_audio);
        var other = new FakeProvisioner(OtherProvider) { RefuseUploads = true };
        var service = CreateService(mediaStore, [other]);
        var item = new VoiceMediaItem { ItemId = "media-1", MasterStorageReference = masterReference };

        // Act
        var result = await service.EnsureProvisionedAsync(item, OtherProvider, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(VoiceMediaProvisioningStatus.Unavailable, result.Status);
        Assert.Null(result.Reference);
        Assert.False(result.Changed);
        Assert.Empty(item.ProviderReferences);
    }

    [Fact]
    public async Task EnsureProvisionedAsync_ForAClipWithNoMasterCopy_PlaysTheCopyTheSameProviderWasGiven_AndFlagsIt()
    {
        // Arrange
        // A clip uploaded before master copies were kept. Nothing can be checked or made again, but what the
        // provider was given still plays, and the status tells the UI to ask for a re-upload.
        var telnyx = new FakeProvisioner(Telnyx);
        var service = CreateService(new FakeMediaStore(), [telnyx]);
        var item = new VoiceMediaItem
        {
            ItemId = "media-1",
            ProviderReferences = new Dictionary<string, string> { [Telnyx] = "legacy-copy" },
        };

        // Act
        var result = await service.EnsureProvisionedAsync(item, Telnyx, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(VoiceMediaProvisioningStatus.ProviderCopyOnly, result.Status);
        Assert.Equal("legacy-copy", result.Reference);
        Assert.False(result.Changed);
        Assert.Empty(telnyx.Uploads);
        Assert.Empty(telnyx.AvailabilityChecks);
    }

    [Fact]
    public async Task EnsureProvisionedAsync_ForAClipWithNoMasterCopy_OnAnotherProvider_ReportsNoAudio()
    {
        // Arrange
        // After a change of provider the same clip has nothing the new provider can play. That is said plainly
        // rather than handing the provider a reference from another account.
        var other = new FakeProvisioner(OtherProvider);
        var service = CreateService(new FakeMediaStore(), [other]);
        var item = new VoiceMediaItem
        {
            ItemId = "media-1",
            ProviderReferences = new Dictionary<string, string> { [Telnyx] = "legacy-copy" },
        };

        // Act
        var result = await service.EnsureProvisionedAsync(item, OtherProvider, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(VoiceMediaProvisioningStatus.NoAudio, result.Status);
        Assert.Null(result.Reference);
        Assert.Empty(other.Uploads);
    }

    [Fact]
    public async Task ResolveAsync_LoadsTheEntry_StoresItWhenACopyWasMade_AndKeepsTheOldMembersInStep()
    {
        // Arrange
        var mediaStore = new FakeMediaStore();
        var masterReference = mediaStore.Add(_audio);
        var other = new FakeProvisioner(OtherProvider);
        var item = new VoiceMediaItem
        {
            ItemId = "media-1",
            MasterStorageReference = masterReference,
            ProviderName = Telnyx,
            MediaReference = "telnyx-copy",
            ProviderReferences = new Dictionary<string, string> { [Telnyx] = "telnyx-copy" },
        };

        var store = new Mock<IVoiceMediaItemStore>();
        store.Setup(value => value.FindByIdAsync("media-1", It.IsAny<CancellationToken>())).ReturnsAsync(item);
        store.Setup(value => value.UpdateAsync(item, It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        var service = CreateService(mediaStore, [other], store);

        // Act
        var resolution = await service.ResolveAsync("media-1", OtherProvider, TestContext.Current.CancellationToken);

        // Assert
        var upload = Assert.Single(other.Uploads);
        Assert.Same(item, resolution.Item);
        Assert.Equal(upload.Reference, resolution.Reference);
        Assert.Equal(VoiceMediaProvisioningStatus.Provisioned, resolution.Status);
        store.Verify(value => value.UpdateAsync(item, It.IsAny<CancellationToken>()), Times.Once);

        // A reader of the single-copy members from before finds the copy a provider was last given.
        Assert.Equal(OtherProvider, item.ProviderName);
        Assert.Equal(upload.Reference, item.MediaReference);
    }

    [Fact]
    public async Task ResolveAsync_WhenNothingWasUploaded_DoesNotStoreTheEntry()
    {
        // Arrange
        var mediaStore = new FakeMediaStore();
        var masterReference = mediaStore.Add(_audio);
        var telnyx = new FakeProvisioner(Telnyx);
        var item = new VoiceMediaItem
        {
            ItemId = "media-1",
            MasterStorageReference = masterReference,
            ProviderReferences = new Dictionary<string, string> { [Telnyx] = "telnyx-copy" },
        };

        var store = new Mock<IVoiceMediaItemStore>(MockBehavior.Strict);
        store.Setup(value => value.FindByIdAsync("media-1", It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var service = CreateService(mediaStore, [telnyx], store);

        // Act
        var resolution = await service.ResolveAsync("media-1", Telnyx, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("telnyx-copy", resolution.Reference);
        store.Verify(value => value.UpdateAsync(It.IsAny<VoiceMediaItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_ForAnUnknownEntry_ReportsNoAudioWithoutAnEntry()
    {
        // Arrange
        var store = new Mock<IVoiceMediaItemStore>();
        store.Setup(value => value.FindByIdAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((VoiceMediaItem)null);

        var service = CreateService(new FakeMediaStore(), [new FakeProvisioner(Telnyx)], store);

        // Act
        var resolution = await service.ResolveAsync("missing", Telnyx, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(resolution.Item);
        Assert.Null(resolution.Reference);
        Assert.Equal(VoiceMediaProvisioningStatus.NoAudio, resolution.Status);
    }

    [Fact]
    public async Task ResyncAsync_VerifiesTheProvidersThatHoldTheClip_AndSendsItToThoseThatDoNot()
    {
        // Arrange
        var mediaStore = new FakeMediaStore();
        var masterReference = mediaStore.Add(_audio);
        var telnyx = new FakeProvisioner(Telnyx);
        var other = new FakeProvisioner(OtherProvider);
        var item = new VoiceMediaItem
        {
            ItemId = "media-1",
            MasterStorageReference = masterReference,
            ProviderReferences = new Dictionary<string, string> { [Telnyx] = "telnyx-copy" },
        };

        var store = new Mock<IVoiceMediaItemStore>();
        store.Setup(value => value.UpdateAsync(item, It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        var service = CreateService(mediaStore, [telnyx, other], store);

        // Act
        var result = await service.ResyncAsync(item, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.HasMasterCopy);
        Assert.Equal(VoiceMediaResyncOutcome.Verified, result.Providers[Telnyx]);
        Assert.Equal(VoiceMediaResyncOutcome.Uploaded, result.Providers[OtherProvider]);
        Assert.Empty(telnyx.Uploads);
        Assert.Equal(Assert.Single(other.Uploads).Reference, item.ProviderReferences[OtherProvider]);
        store.Verify(value => value.UpdateAsync(item, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResyncAsync_WithoutAMasterCopy_SaysSoAndTouchesNothing()
    {
        // Arrange
        var telnyx = new FakeProvisioner(Telnyx);
        var store = new Mock<IVoiceMediaItemStore>(MockBehavior.Strict);
        var service = CreateService(new FakeMediaStore(), [telnyx], store);
        var item = new VoiceMediaItem
        {
            ItemId = "media-1",
            ProviderReferences = new Dictionary<string, string> { [Telnyx] = "legacy-copy" },
        };

        // Act
        var result = await service.ResyncAsync(item, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.HasMasterCopy);
        Assert.Empty(result.Providers);
        Assert.Empty(telnyx.Uploads);
    }

    [Fact]
    public async Task DeleteEverywhereAsync_RemovesTheMasterCopyAndEveryProviderCopy()
    {
        // Arrange
        var mediaStore = new FakeMediaStore();
        var masterReference = mediaStore.Add(_audio);
        var telnyx = new FakeProvisioner(Telnyx);
        var other = new FakeProvisioner(OtherProvider);
        var service = CreateService(mediaStore, [telnyx, other]);
        var item = new VoiceMediaItem
        {
            ItemId = "media-1",
            MasterStorageReference = masterReference,
            ProviderReferences = new Dictionary<string, string>
            {
                [Telnyx] = "telnyx-copy",
                [OtherProvider] = "other-copy",
            },
        };

        // Act
        await service.DeleteEverywhereAsync(item, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(mediaStore.Read(masterReference));
        Assert.Equal(new[] { "telnyx-copy" }, telnyx.Deleted);
        Assert.Equal(new[] { "other-copy" }, other.Deleted);
    }

    [Fact]
    public async Task DeleteEverywhereAsync_WhenOneProviderWillNotDelete_StillRemovesTheRest()
    {
        // Arrange
        // A provider that is down must not keep an operator from deleting an entry, nor leave the other copies behind.
        var mediaStore = new FakeMediaStore();
        var masterReference = mediaStore.Add(_audio);
        var telnyx = new FakeProvisioner(Telnyx) { FailDeletes = true };
        var other = new FakeProvisioner(OtherProvider);
        var service = CreateService(mediaStore, [telnyx, other]);
        var item = new VoiceMediaItem
        {
            ItemId = "media-1",
            MasterStorageReference = masterReference,
            ProviderReferences = new Dictionary<string, string>
            {
                [Telnyx] = "telnyx-copy",
                [OtherProvider] = "other-copy",
            },
        };

        // Act
        await service.DeleteEverywhereAsync(item, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(mediaStore.Read(masterReference));
        Assert.Equal(new[] { "other-copy" }, other.Deleted);
    }

    [Fact]
    public void ActiveProviderName_IsTheTenantsDefaultProvider_WhenItHostsMedia()
    {
        // Arrange
        var telnyx = new FakeProvisioner(Telnyx);
        var other = new FakeProvisioner(OtherProvider);

        // Act
        var service = CreateService(new FakeMediaStore(), [telnyx, other], defaultProviderName: OtherProvider);

        // Assert
        Assert.Equal(OtherProvider, service.ActiveProviderName);
        Assert.True(service.CanUpload);
    }

    private static VoiceMediaProvisioningService CreateService(
        IRecordingMediaStore mediaStore,
        IVoiceMediaProvisioner[] provisioners,
        Mock<IVoiceMediaItemStore> store = null,
        string defaultProviderName = null)
    {
        var settings = new Mock<IOptionsSnapshot<TelephonySettings>>();
        settings.SetupGet(value => value.Value).Returns(new TelephonySettings { DefaultProviderName = defaultProviderName });

        return new VoiceMediaProvisioningService(
            (store ?? new Mock<IVoiceMediaItemStore>()).Object,
            mediaStore is null ? Array.Empty<IRecordingMediaStore>() : new[] { mediaStore },
            provisioners,
            settings.Object,
            NullLogger<VoiceMediaProvisioningService>.Instance);
    }

    private sealed class FakeMediaStore : IRecordingMediaStore
    {
        private readonly Dictionary<string, byte[]> _media = new(StringComparer.Ordinal);

        public string Add(byte[] content)
        {
            var key = $"master-{_media.Count + 1}";
            _media[key] = content;

            return key;
        }

        public byte[] Read(string storageReference)
            => _media.TryGetValue(storageReference, out var content) ? content : null;

        public async Task<string> StoreAsync(RecordingMediaWriteRequest request, CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            await request.Content.CopyToAsync(buffer, cancellationToken);
            _media[request.StorageKey] = buffer.ToArray();

            return request.StorageKey;
        }

        public Task<Stream> OpenReadAsync(string storageReference, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream>(_media.TryGetValue(storageReference, out var content) ? new MemoryStream(content) : null);

        public Task<bool> DeleteAsync(string storageReference, CancellationToken cancellationToken = default)
        {
            _media.Remove(storageReference);

            return Task.FromResult(true);
        }
    }

    private sealed class FakeProvisioner : IVoiceMediaProvisioner
    {
        public FakeProvisioner(string providerTechnicalName)
        {
            ProviderTechnicalName = providerTechnicalName;
        }

        public string ProviderTechnicalName { get; }

        public bool RefuseUploads { get; init; }

        public bool FailDeletes { get; init; }

        public Func<string, VoiceMediaAvailability> Availability { get; init; } = _ => VoiceMediaAvailability.Available;

        public List<(byte[] Content, string ContentType, string NamePrefix, string Reference)> Uploads { get; } = [];

        public List<string> AvailabilityChecks { get; } = [];

        public List<string> Deleted { get; } = [];

        public async Task<string> UploadAsync(Stream audio, string contentType, string namePrefix, CancellationToken cancellationToken = default)
        {
            if (RefuseUploads)
            {
                return null;
            }

            using var buffer = new MemoryStream();
            await audio.CopyToAsync(buffer, cancellationToken);

            var reference = $"{namePrefix}-{ProviderTechnicalName}-{Uploads.Count + 1}";
            Uploads.Add((buffer.ToArray(), contentType, namePrefix, reference));

            return reference;
        }

        public Task<VoiceMediaAvailability> GetAvailabilityAsync(string mediaReference, CancellationToken cancellationToken = default)
        {
            AvailabilityChecks.Add(mediaReference);

            return Task.FromResult(Availability(mediaReference));
        }

        public Task DeleteAsync(string mediaReference, CancellationToken cancellationToken = default)
        {
            if (FailDeletes)
            {
                throw new HttpRequestException("The provider is unreachable.");
            }

            Deleted.Add(mediaReference);

            return Task.CompletedTask;
        }
    }

    // What an upload arrives as when the request body is not buffered: readable once, from the start, no seeking.
    private sealed class ForwardOnlyStream : Stream
    {
        private readonly MemoryStream _inner;

        public ForwardOnlyStream(byte[] content)
        {
            _inner = new MemoryStream(content);
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
    }
}
