using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements.Handlers;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentManagement.Metadata.Records;
using OrchardCore.ContentTypes.Events;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Handlers;

public sealed class OmnichannelContactDefinitionHandlerTests
{
    [Fact]
    public void ContentTypeBuilding_WhenOmnichannelContactPartExists_ShouldInjectContactMethodsBag()
    {
        var handler = new OmnichannelContactDefinitionHandler();
        var context = new ContentTypeBuildingContext("Customer", new ContentTypeDefinitionRecord
        {
            Name = "Customer",
            ContentTypePartDefinitionRecords =
            [
                new ContentTypePartDefinitionRecord
                {
                    Name = OmnichannelConstants.ContentParts.OmnichannelContact,
                    PartName = OmnichannelConstants.ContentParts.OmnichannelContact,
                    Settings = new JsonObject(),
                },
            ],
            Settings = new JsonObject(),
        });

        handler.ContentTypeBuilding(context);

        var contactMethods = Assert.Single(context.Record.ContentTypePartDefinitionRecords.Where(record =>
            string.Equals(record.Name, OmnichannelConstants.NamedParts.ContactMethods, StringComparison.Ordinal)));

        Assert.Equal("BagPart", contactMethods.PartName);
        Assert.Equal(
            OmnichannelContactDefinitionService.ContactMethodsDisplayName,
            contactMethods.Settings[nameof(ContentTypePartSettings)]?["DisplayName"]?.GetValue<string>());
        Assert.True(contactMethods.Settings[nameof(ContentSettings)]?["IsSystemDefined"]?.GetValue<bool>());
        Assert.Equal(
            OmnichannelConstants.Sterotypes.ContactMethod,
            contactMethods.Settings[nameof(BagPartSettings)]?["ContainedStereotypes"]?[0]?.GetValue<string>());
    }

    [Fact]
    public void ContentTypeBuilding_WhenIncorrectLegacyAliasExists_ShouldReplaceIt()
    {
        var handler = new OmnichannelContactDefinitionHandler();
        var context = new ContentTypeBuildingContext("Customer", new ContentTypeDefinitionRecord
        {
            Name = "Customer",
            ContentTypePartDefinitionRecords =
            [
                new ContentTypePartDefinitionRecord
                {
                    Name = OmnichannelConstants.ContentParts.OmnichannelContact,
                    PartName = OmnichannelConstants.ContentParts.OmnichannelContact,
                    Settings = new JsonObject(),
                },
                new ContentTypePartDefinitionRecord
                {
                    Name = "BagPart",
                    PartName = OmnichannelConstants.NamedParts.ContactMethods,
                    Settings = new JsonObject(),
                },
            ],
            Settings = new JsonObject(),
        });

        handler.ContentTypeBuilding(context);

        Assert.DoesNotContain(context.Record.ContentTypePartDefinitionRecords, record =>
            string.Equals(record.Name, "BagPart", StringComparison.Ordinal) &&
            string.Equals(record.PartName, OmnichannelConstants.NamedParts.ContactMethods, StringComparison.Ordinal));

        Assert.Contains(context.Record.ContentTypePartDefinitionRecords, record =>
            string.Equals(record.Name, OmnichannelConstants.NamedParts.ContactMethods, StringComparison.Ordinal) &&
            string.Equals(record.PartName, "BagPart", StringComparison.Ordinal));
    }
}
