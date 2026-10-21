using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Handlers;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Metadata.Records;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentTypes.Events;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Handlers;

public sealed class OmnichannelContactDefinitionHandlerTests
{
    [Fact]
    public void ContentTypeBuilding_WhenOmnichannelContactPartExists_ShouldInjectContactMethodsBag()
    {
        var handler = new OmnichannelContactDefinitionHandler(TestContactMethods.Accessor);
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

        var contactMethods = Assert.Single(
            context.Record.ContentTypePartDefinitionRecords,
            record => string.Equals(record.Name, OmnichannelConstants.NamedParts.ContactMethods, StringComparison.Ordinal));

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
        var handler = new OmnichannelContactDefinitionHandler(TestContactMethods.Accessor);
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

    [Fact]
    public void ContentTypeBuilding_WhenTheTypeHasAContactMethodsProvider_DoesNotAddTheBag()
    {
        // Arrange
        var handler = new OmnichannelContactDefinitionHandler(CreateAccessorWithProviderFor("Person"));
        var context = new ContentTypeBuildingContext("Person", new ContentTypeDefinitionRecord
        {
            Name = "Person",
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

        // Act
        handler.ContentTypeBuilding(context);

        // Assert
        Assert.DoesNotContain(context.Record.ContentTypePartDefinitionRecords, record =>
            string.Equals(record.Name, OmnichannelConstants.NamedParts.ContactMethods, StringComparison.Ordinal));
    }

    [Fact]
    public void ContentTypeBuilding_WhenATypeWithAProviderAlreadyHasTheBag_LeavesItExactlyAsItIs()
    {
        // Arrange
        var handler = new OmnichannelContactDefinitionHandler(CreateAccessorWithProviderFor("Person"));
        var bagSettings = new JsonObject
        {
            ["Custom"] = "kept",
        };
        var context = new ContentTypeBuildingContext("Person", new ContentTypeDefinitionRecord
        {
            Name = "Person",
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
                    Name = OmnichannelConstants.NamedParts.ContactMethods,
                    PartName = "BagPart",
                    Settings = bagSettings,
                },
            ],
            Settings = new JsonObject(),
        });

        // Act
        handler.ContentTypeBuilding(context);

        // Assert
        var contactMethods = Assert.Single(
            context.Record.ContentTypePartDefinitionRecords,
            record => string.Equals(record.Name, OmnichannelConstants.NamedParts.ContactMethods, StringComparison.Ordinal));
        Assert.Equal("BagPart", contactMethods.PartName);
        Assert.Same(bagSettings, contactMethods.Settings);
        Assert.Equal("kept", contactMethods.Settings["Custom"]?.GetValue<string>());
    }

    private static IContactMethodsAccessor CreateAccessorWithProviderFor(string contentType)
        => TestContactMethods
            .CreateServices(services => services.AddContactMethodsProvider<FieldContactMethodsProvider>(contentType))
            .GetRequiredService<IContactMethodsAccessor>();
}
