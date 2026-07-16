using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Reports;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

public sealed class CatalogReportDisplayNamesTests
{
    [Fact]
    public void Resolve_KnownCampaign_ReturnsDisplayText()
    {
        // Arrange
        var names = CatalogReportDisplayNames.ForCampaigns(
        [
            new OmnichannelCampaign
            {
                ItemId = "campaign-id",
                DisplayText = "Customer renewal",
            },
        ]);

        // Act
        var result = CatalogReportDisplayNames.Resolve(
            "campaign-id",
            names,
            "(No campaign)",
            "(Unknown campaign)");

        // Assert
        Assert.Equal("Customer renewal", result);
    }

    [Fact]
    public void Resolve_UnknownDisposition_ReturnsUnknownTextWithoutExposingIdentifier()
    {
        // Act
        var result = CatalogReportDisplayNames.Resolve(
            "disposition-id",
            new Dictionary<string, string>(StringComparer.Ordinal),
            "(No disposition)",
            "(Unknown disposition)");

        // Assert
        Assert.Equal("(Unknown disposition)", result);
    }
}
