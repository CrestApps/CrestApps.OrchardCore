using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed class OmnichannelContactDefinitionServiceTests
{
    [Fact]
    public void HasOmnichannelContactPart_WhenTypeContainsOmnichannelContactPart_ShouldReturnTrue()
    {
        var contentTypeDefinition = CreateContentTypeDefinition(
            "Customer",
            CreateTypePartDefinition(OmnichannelConstants.ContentParts.OmnichannelContact, OmnichannelConstants.ContentParts.OmnichannelContact));

        var result = OmnichannelContactDefinitionService.HasOmnichannelContactPart(contentTypeDefinition);

        Assert.True(result);
    }

    [Fact]
    public void NeedsContactMethodsBagUpdate_WhenContactMethodsBagIsMissing_ShouldReturnTrue()
    {
        var contentTypeDefinition = CreateContentTypeDefinition(
            "Customer",
            CreateTypePartDefinition(OmnichannelConstants.ContentParts.OmnichannelContact, OmnichannelConstants.ContentParts.OmnichannelContact));

        var result = OmnichannelContactDefinitionService.NeedsContactMethodsBagUpdate(contentTypeDefinition);

        Assert.True(result);
    }

    [Fact]
    public void NeedsContactMethodsBagUpdate_WhenContactMethodsBagMatchesInvariant_ShouldReturnFalse()
    {
        var contentTypeDefinition = CreateContentTypeDefinition(
            "Customer",
            CreateTypePartDefinition(OmnichannelConstants.ContentParts.OmnichannelContact, OmnichannelConstants.ContentParts.OmnichannelContact),
            CreateContactMethodsTypePartDefinition());

        var result = OmnichannelContactDefinitionService.NeedsContactMethodsBagUpdate(contentTypeDefinition);

        Assert.False(result);
    }

    [Fact]
    public void NeedsContactMethodsBagUpdate_WhenBagAliasIsIncorrect_ShouldReturnTrue()
    {
        var contentTypeDefinition = CreateContentTypeDefinition(
            "Customer",
            CreateTypePartDefinition(OmnichannelConstants.ContentParts.OmnichannelContact, OmnichannelConstants.ContentParts.OmnichannelContact),
            CreateTypePartDefinition("BagPart", OmnichannelConstants.NamedParts.ContactMethods));

        var result = OmnichannelContactDefinitionService.NeedsContactMethodsBagUpdate(contentTypeDefinition);

        Assert.True(result);
    }

    private static ContentTypeDefinition CreateContentTypeDefinition(string name, params ContentTypePartDefinition[] parts)
    {
        var contentTypeDefinition = new ContentTypeDefinition(name, name, parts, new JsonObject());

        foreach (var part in parts)
        {
            part.ContentTypeDefinition = contentTypeDefinition;
        }

        return contentTypeDefinition;
    }

    private static ContentTypePartDefinition CreateTypePartDefinition(string partName, string partDefinitionName)
        => new(partName, new ContentPartDefinition(partDefinitionName), new JsonObject());

    private static ContentTypePartDefinition CreateContactMethodsTypePartDefinition()
        => new(
            OmnichannelConstants.NamedParts.ContactMethods,
            new ContentPartDefinition("BagPart"),
            new JsonObject
            {
                ["ContentTypePartSettings"] = new JsonObject
                {
                    ["DisplayName"] = OmnichannelContactDefinitionService.ContactMethodsDisplayName,
                    ["Description"] = OmnichannelContactDefinitionService.ContactMethodsDescription,
                    ["Position"] = OmnichannelContactDefinitionService.ContactMethodsPosition,
                },
                ["ContentSettings"] = new JsonObject
                {
                    ["IsSystemDefined"] = true,
                },
                ["BagPartSettings"] = new JsonObject
                {
                    ["ContainedContentTypes"] = new JsonArray(),
                    ["ContainedStereotypes"] = new JsonArray(OmnichannelConstants.Sterotypes.ContactMethod),
                    ["CollapseContainedItems"] = false,
                },
            });
}
