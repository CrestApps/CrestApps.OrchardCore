using CrestApps.OrchardCore.Wizard.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Wizard.Contents;

/// <summary>
/// Enforces the per-wizard <see cref="WizardPartSettings.RequiresAuthenticatedUser"/> setting for
/// content-driven wizards, which the type-level <see cref="ContentWizardDefinition"/> cannot express.
/// </summary>
public sealed class ContentWizardAccessPolicy : IWizardAccessPolicy
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentWizardAccessPolicy"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve content services lazily.</param>
    public ContentWizardAccessPolicy(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task<bool> RequiresAuthenticatedUserAsync(string wizardType, string definitionId)
    {
        if (!string.Equals(wizardType, ContentWizardConstants.WizardType, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(definitionId))
        {
            return false;
        }

        var contentManager = _serviceProvider.GetRequiredService<IContentManager>();

        var contentItem = await contentManager.GetAsync(definitionId);

        if (contentItem == null)
        {
            return false;
        }

        var contentDefinitionManager = _serviceProvider.GetRequiredService<IContentDefinitionManager>();

        var typeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

        var partDefinition = typeDefinition?.Parts.FirstOrDefault(part =>
            string.Equals(part.PartDefinition.Name, nameof(WizardPart), StringComparison.Ordinal));

        return partDefinition?.GetSettings<WizardPartSettings>().RequiresAuthenticatedUser == true;
    }
}
