using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.ViewModels;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelContactDisplayDriver : ContentDisplayDriver
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    private readonly Dictionary<string, ContentTypeDefinition> _evaluatedContentTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ContentTypeDefinition> _contactWithHeaderContentTypes = new(StringComparer.OrdinalIgnoreCase);

    public OmnichannelContactDisplayDriver(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public override bool CanHandleModel(ContentItem contentItem)
    {
        return contentItem.Has<OmnichannelContactPart>();
    }

    public override IDisplayResult Display(ContentItem contentItem, BuildDisplayContext context)
    {
        return Initialize<ContentItemViewModel>("ContactSummaryAdmin", model =>
        {
            model.ContentItem = contentItem;
        }).Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:3");
    }

    public override async Task<IDisplayResult> EditAsync(ContentItem contentItem, BuildEditorContext context)
    {
        if (string.IsNullOrEmpty(contentItem.ContentType))
        {
            return null;
        }

        if (!_evaluatedContentTypes.TryGetValue(contentItem.ContentType, out var contentTypeDefinition))
        {
            contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

            if (contentTypeDefinition is null)
            {
                return null;
            }

            _evaluatedContentTypes[contentItem.ContentType] = contentTypeDefinition;

            if (contentTypeDefinition.Parts.Any(x => x.Name == "ListPart"))
            {
                // When a content has a 'ListPart', we need a way to inject "List Activities" button into the 'ListPartNavigationAdmin' in OrchardCore.
                return null;
            }

            _contactWithHeaderContentTypes[contentItem.ContentType] = contentTypeDefinition;
        }
        else if (!_contactWithHeaderContentTypes.TryGetValue(contentItem.ContentType, out contentTypeDefinition))
        {
            return null;
        }

        return Shape("ContactNavigationAdmin", new ContactNavigationAdminShapeViewModel()
        {
            ContactContentItem = contentItem,
            Definition = contentTypeDefinition,
            ShowEdit = false,
        }).Location("Content:1.5")
        .RenderWhen(() => Task.FromResult(!context.IsNew));
    }
}
