using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed class OmnichannelSubjectDefinitionServiceTests
{
    [Fact]
    public void HasOmnichannelSubjectPart_WhenTypeContainsMarkerPart_ShouldReturnTrue()
    {
        // Arrange
        var subjectPart = new ContentTypePartDefinition(
            OmnichannelConstants.ContentParts.OmnichannelSubject,
            new ContentPartDefinition(OmnichannelConstants.ContentParts.OmnichannelSubject),
            new JsonObject());
        var contentTypeDefinition = new ContentTypeDefinition(
            "Renewal",
            "Renewal",
            [subjectPart],
            new JsonObject());

        // Act
        var result = OmnichannelSubjectDefinitionService.HasOmnichannelSubjectPart(contentTypeDefinition);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasOmnichannelSubjectPart_WhenTypeDoesNotContainMarkerPart_ShouldReturnFalse()
    {
        // Arrange
        var contentTypeDefinition = new ContentTypeDefinition("Renewal", "Renewal");

        // Act
        var result = OmnichannelSubjectDefinitionService.HasOmnichannelSubjectPart(contentTypeDefinition);

        // Assert
        Assert.False(result);
    }
}
