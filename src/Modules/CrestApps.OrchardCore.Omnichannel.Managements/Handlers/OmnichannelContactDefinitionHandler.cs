using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using OrchardCore.ContentManagement.Metadata.Records;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentTypes.Events;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

internal sealed class OmnichannelContactDefinitionHandler : IContentDefinitionHandler
{
    public void ContentTypeBuilding(ContentTypeBuildingContext context)
    {
        if (context?.Record is null || !HasOmnichannelContactPart(context.Record))
        {
            return;
        }

        var incorrectContactMethodsRecord = context.Record.ContentTypePartDefinitionRecords.FirstOrDefault(record =>
            string.Equals(record.Name, "BagPart", StringComparison.Ordinal) &&
            string.Equals(record.PartName, OmnichannelConstants.NamedParts.ContactMethods, StringComparison.Ordinal));

        if (incorrectContactMethodsRecord is not null)
        {
            context.Record.ContentTypePartDefinitionRecords.Remove(incorrectContactMethodsRecord);
        }

        var contactMethodsRecord = context.Record.ContentTypePartDefinitionRecords.FirstOrDefault(record =>
            string.Equals(record.Name, OmnichannelConstants.NamedParts.ContactMethods, StringComparison.Ordinal));

        if (contactMethodsRecord is null)
        {
            context.Record.ContentTypePartDefinitionRecords.Add(new ContentTypePartDefinitionRecord
            {
                Name = OmnichannelConstants.NamedParts.ContactMethods,
                PartName = "BagPart",
                Settings = CreateContactMethodsSettings(),
            });

            return;
        }

        contactMethodsRecord.PartName = "BagPart";
        contactMethodsRecord.Settings = CreateContactMethodsSettings();
    }

    public void ContentTypePartBuilding(ContentTypePartBuildingContext context)
    {
        if (context?.Record is null ||
            !string.Equals(context.Record.Name, OmnichannelConstants.NamedParts.ContactMethods, StringComparison.Ordinal) ||
            !string.Equals(context.Record.PartName, "BagPart", StringComparison.Ordinal))
        {
            return;
        }

        context.Record.Settings = CreateContactMethodsSettings();
    }

    public void ContentPartBuilding(ContentPartBuildingContext context)
    {
    }

    public void ContentPartFieldBuilding(ContentPartFieldBuildingContext context)
    {
    }

    private static bool HasOmnichannelContactPart(ContentTypeDefinitionRecord contentTypeDefinitionRecord)
    {
        return contentTypeDefinitionRecord.ContentTypePartDefinitionRecords.Any(record =>
            string.Equals(record.Name, OmnichannelConstants.ContentParts.OmnichannelContact, StringComparison.Ordinal) ||
            string.Equals(record.PartName, OmnichannelConstants.ContentParts.OmnichannelContact, StringComparison.Ordinal));
    }

    private static JsonObject CreateContactMethodsSettings()
    {
        return new JsonObject
        {
            [nameof(ContentTypePartSettings)] = JObject.FromObject(new ContentTypePartSettings
            {
                DisplayName = OmnichannelContactDefinitionService.ContactMethodsDisplayName,
                Description = OmnichannelContactDefinitionService.ContactMethodsDescription,
                Position = OmnichannelContactDefinitionService.ContactMethodsPosition,
            }),
            [nameof(ContentSettings)] = JObject.FromObject(new ContentSettings
            {
                IsSystemDefined = true,
            }),
            [nameof(BagPartSettings)] = JObject.FromObject(new BagPartSettings
            {
                ContainedStereotypes = [OmnichannelConstants.Sterotypes.ContactMethod],
            }),
        };
    }
}
