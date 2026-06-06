using CrestApps.OrchardCore.ContentTransfer;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Modules.ContentTransfer;

public sealed class ContentTransferEntryStatusExtensionsTests
{
    [Theory]
    [InlineData(ContentTransferEntryStatus.New)]
    [InlineData(ContentTransferEntryStatus.Pending)]
    public void IsPendingImport_WhenStatusIsPendingLike_ShouldReturnTrue(ContentTransferEntryStatus status)
        => Assert.True(status.IsPendingImport());

    [Theory]
    [InlineData(ContentTransferEntryStatus.Paused)]
    public void IsPausedImport_WhenStatusIsPausedLike_ShouldReturnTrue(ContentTransferEntryStatus status)
        => Assert.True(status.IsPausedImport());

    [Theory]
    [InlineData(ContentTransferEntryStatus.New, ContentTransferEntryStatus.Pending)]
    [InlineData(ContentTransferEntryStatus.Pending, ContentTransferEntryStatus.Pending)]
    [InlineData(ContentTransferEntryStatus.Paused, ContentTransferEntryStatus.Paused)]
    public void NormalizeImportStatus_WhenStatusIsLegacyImportState_ShouldReturnModernStatus(
        ContentTransferEntryStatus status,
        ContentTransferEntryStatus expected)
        => Assert.Equal(expected, status.NormalizeImportStatus());

    [Theory]
    [InlineData(ContentTransferEntryStatus.Paused)]
    [InlineData(ContentTransferEntryStatus.Deleting)]
    public void ShouldStopImport_WhenStatusStopsProcessing_ShouldReturnTrue(ContentTransferEntryStatus status)
        => Assert.True(status.ShouldStopImport());
}
