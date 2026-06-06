using CrestApps.OrchardCore.Omnichannel.Core;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

internal sealed class OmnichannelContactDefinitionService
{
    internal const string ContactMethodsDisplayName = "Contact Methods";
    internal const string ContactMethodsDescription = "Stores the contact methods for omnichannel contacts.";
    internal const string ContactMethodsPosition = "100";

    private readonly IContentDefinitionManager _contentDefinitionManager;

    public OmnichannelContactDefinitionService(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task EnsureContactMethodsBagAsync(string contentTypeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentTypeName);

        var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentTypeName);

        if (contentTypeDefinition is null || !HasOmnichannelContactPart(contentTypeDefinition) || !NeedsContactMethodsBagUpdate(contentTypeDefinition))
        {
            return;
        }

        await _contentDefinitionManager.AlterTypeDefinitionAsync(contentTypeName, ConfigureContactMethodsBagPart);
    }

    public async Task RepairOmnichannelContactContentTypesAsync()
    {
        var contentTypeDefinitions = await _contentDefinitionManager.ListTypeDefinitionsAsync();

        foreach (var contentTypeDefinition in contentTypeDefinitions)
        {
            if (!HasOmnichannelContactPart(contentTypeDefinition) || !NeedsContactMethodsBagUpdate(contentTypeDefinition))
            {
                continue;
            }

            await EnsureContactMethodsBagAsync(contentTypeDefinition.Name);
        }
    }

    internal static bool HasOmnichannelContactPart(ContentTypeDefinition contentTypeDefinition)
    {
        ArgumentNullException.ThrowIfNull(contentTypeDefinition);

        return contentTypeDefinition.Parts.Any(part =>
            string.Equals(part.Name, OmnichannelConstants.ContentParts.OmnichannelContact, StringComparison.Ordinal) ||
            string.Equals(part.PartDefinition.Name, OmnichannelConstants.ContentParts.OmnichannelContact, StringComparison.Ordinal));
    }

    internal static bool NeedsContactMethodsBagUpdate(ContentTypeDefinition contentTypeDefinition)
    {
        ArgumentNullException.ThrowIfNull(contentTypeDefinition);

        var contactMethodsPart = contentTypeDefinition.Parts.FirstOrDefault(part =>
            string.Equals(part.Name, OmnichannelConstants.NamedParts.ContactMethods, StringComparison.Ordinal));

        if (contactMethodsPart is null || !string.Equals(contactMethodsPart.PartDefinition.Name, "BagPart", StringComparison.Ordinal))
        {
            return true;
        }

        var contentSettings = contactMethodsPart.GetSettings<ContentSettings>();
        var bagPartSettings = contactMethodsPart.GetSettings<BagPartSettings>();

        if (!contentSettings.IsSystemDefined)
        {
            return true;
        }

        return bagPartSettings.ContainedStereotypes is null ||
            bagPartSettings.ContainedStereotypes.Length != 1 ||
            !string.Equals(bagPartSettings.ContainedStereotypes[0], OmnichannelConstants.Sterotypes.ContactMethod, StringComparison.Ordinal);
    }

    internal static void ConfigureContactMethodsBagPart(ContentTypeDefinitionBuilder type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var incorrectlyNamedBagPart = type.Current?.Parts.FirstOrDefault(part =>
            string.Equals(part.Name, "BagPart", StringComparison.Ordinal) &&
            string.Equals(part.PartDefinition.Name, OmnichannelConstants.NamedParts.ContactMethods, StringComparison.Ordinal));

        if (incorrectlyNamedBagPart is not null)
        {
            type.RemovePart(incorrectlyNamedBagPart.Name);
        }

        type.WithPart(OmnichannelConstants.NamedParts.ContactMethods, "BagPart", part => part
            .WithDisplayName(ContactMethodsDisplayName)
            .WithDescription(ContactMethodsDescription)
            .WithPosition(ContactMethodsPosition)
            .WithSettings(new ContentSettings
            {
                IsSystemDefined = true,
            })
            .WithSettings(new BagPartSettings
            {
                ContainedStereotypes = [OmnichannelConstants.Sterotypes.ContactMethod],
            })
        );
    }
}
