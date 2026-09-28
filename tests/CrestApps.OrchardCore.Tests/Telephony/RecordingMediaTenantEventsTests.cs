using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Removing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class RecordingMediaTenantEventsTests
{
    [Fact]
    public async Task RemovingAsync_WhenPurgeCompletes_AllowsTenantRemoval()
    {
        // Arrange
        var mediaStore = new PurgeableRecordingMediaStore
        {
            PurgeResult = true,
        };
        using var fixture = CreateTenantEvents(mediaStore);
        var context = new ShellRemovingContext();

        // Act
        await fixture.TenantEvents.RemovingAsync(context);

        // Assert
        Assert.True(context.Success);
        Assert.Equal(1, mediaStore.PurgeCalls);
    }

    [Fact]
    public async Task RemovingAsync_WhenPurgeDoesNotComplete_BlocksTenantRemoval()
    {
        // Arrange
        var mediaStore = new PurgeableRecordingMediaStore();
        using var fixture = CreateTenantEvents(mediaStore);
        var context = new ShellRemovingContext();

        // Act
        await fixture.TenantEvents.RemovingAsync(context);

        // Assert
        Assert.False(context.Success);
        Assert.Contains("recording media cleanup did not complete", context.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(1, mediaStore.PurgeCalls);
    }

    [Fact]
    public async Task RemovingAsync_WhenPurgeThrows_BlocksTenantRemovalWithError()
    {
        // Arrange
        var expected = new InvalidOperationException("purge failed");
        var mediaStore = new PurgeableRecordingMediaStore
        {
            PurgeException = expected,
        };
        using var fixture = CreateTenantEvents(mediaStore);
        var context = new ShellRemovingContext();

        // Act
        await fixture.TenantEvents.RemovingAsync(context);

        // Assert
        Assert.False(context.Success);
        Assert.Same(expected, context.Error);
        Assert.Contains("recording media cleanup failed", context.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemovingAsync_WhenStoreCannotPurgeTenantMedia_BlocksTenantRemoval()
    {
        // Arrange
        using var fixture = CreateTenantEvents(new RecordingMediaStore());
        var context = new ShellRemovingContext();

        // Act
        await fixture.TenantEvents.RemovingAsync(context);

        // Assert
        Assert.False(context.Success);
        Assert.Contains("does not support tenant-wide media cleanup", context.ErrorMessage, StringComparison.Ordinal);
    }

    private static TenantEventsFixture CreateTenantEvents(IRecordingMediaStore mediaStore)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();
        services.AddSingleton(mediaStore);

        using var serviceProvider = services.BuildServiceProvider();
        var tenantEventsType = typeof(CrestApps.OrchardCore.Telephony.Startup).Assembly.GetType(
            "CrestApps.OrchardCore.Telephony.Services.RecordingMediaTenantEvents",
            throwOnError: true);
        var tenantEvents = (ModularTenantEvents)ActivatorUtilities.CreateInstance(serviceProvider, tenantEventsType);

        return new TenantEventsFixture(serviceProvider, tenantEvents);
    }

    private sealed class TenantEventsFixture(
        ServiceProvider serviceProvider,
        ModularTenantEvents tenantEvents) : IDisposable
    {
        public ModularTenantEvents TenantEvents { get; } = tenantEvents;

        public void Dispose()
        {
            serviceProvider.Dispose();
        }
    }

    private class RecordingMediaStore : IRecordingMediaStore
    {
        public Task<string> StoreAsync(
            RecordingMediaWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(request.StorageKey);
        }

        public Task<Stream> OpenReadAsync(
            string storageReference,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Stream>(null);
        }

        public Task<bool> DeleteAsync(
            string storageReference,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }
    }

    private sealed class PurgeableRecordingMediaStore : RecordingMediaStore, ISupportsTenantMediaPurge
    {
        public bool PurgeResult { get; set; }

        public Exception PurgeException { get; set; }

        public int PurgeCalls { get; private set; }

        public Task<bool> TryPurgeAllAsync(CancellationToken cancellationToken = default)
        {
            PurgeCalls++;

            if (PurgeException is not null)
            {
                throw PurgeException;
            }

            return Task.FromResult(PurgeResult);
        }
    }
}
