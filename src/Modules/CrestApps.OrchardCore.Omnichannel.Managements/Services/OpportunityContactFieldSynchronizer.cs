using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.ContentFields.Settings;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Keeps the <c>PrimaryContact</c> field of the opportunity part offering the current contact types. Which types sit
/// in an account's list is kept by the Accounts feature, from <see cref="CrmAccountChildTypeRule"/>.
/// </summary>
internal sealed class OpportunityContactFieldSynchronizer
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpportunityContactFieldSynchronizer"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public OpportunityContactFieldSynchronizer(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Brings the primary contact field up to date with the current contact types.
    /// </summary>
    public async Task SynchronizeAsync()
    {
        // The primary contact of an opportunity is picked from the contact types, which change as types are defined.
        var definitions = await _contentDefinitionManager.ListTypeDefinitionsAsync();
        var partDefinition = await _contentDefinitionManager.GetPartDefinitionAsync(OmnichannelConstants.ContentParts.Opportunity);
        var fieldDefinition = partDefinition?.Fields.FirstOrDefault(field => field.Name == nameof(OpportunityPart.PrimaryContact));

        if (fieldDefinition is null)
        {
            return;
        }

        var contactTypes = definitions
            .Where(OmnichannelRecordKinds.IsContact)
            .Select(definition => definition.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var settings = fieldDefinition.GetSettings<ContentPickerFieldSettings>();

        if ((settings.DisplayedContentTypes ?? []).Order(StringComparer.Ordinal).SequenceEqual(contactTypes, StringComparer.Ordinal))
        {
            return;
        }

        await _contentDefinitionManager.AlterPartDefinitionAsync(OmnichannelConstants.ContentParts.Opportunity, part => part
            .WithField(fieldDefinition.Name, field => field
                .MergeSettings<ContentPickerFieldSettings>(fieldSettings => fieldSettings.DisplayedContentTypes = contactTypes)));
    }
}
