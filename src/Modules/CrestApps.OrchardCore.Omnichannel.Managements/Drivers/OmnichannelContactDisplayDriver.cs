using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
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
    private const string HeaderAdminDisplayType = "HeaderAdmin";

    private readonly IContentDefinitionManager _contentDefinitionManager;

    private readonly Dictionary<string, ContentTypeDefinition> _evaluatedContentTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ContentTypeDefinition> _contactWithHeaderContentTypes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactDisplayDriver"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public OmnichannelContactDisplayDriver(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public override bool CanHandleModel(ContentItem contentItem)
    {
        return contentItem.Has<OmnichannelContactPart>();
    }

    public override async Task<IDisplayResult> DisplayAsync(ContentItem contentItem, BuildDisplayContext context)
    {
        var results = new List<IDisplayResult>
        {
            Initialize<ContentItemViewModel>("ContactSummaryAdmin", model =>
            {
                model.ContentItem = contentItem;
            }).Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:3"),

            // The list part's header renders its container in the HeaderAdmin display type, so this puts the
            // primary contact methods under the contact's name whenever the list header is shown.
            Initialize<ContactPrimaryMethodsViewModel>("ContactPrimaryMethods", model =>
            {
                model.Methods = ContactPrimaryMethodsResolver.Resolve(contentItem);
            }).Location(HeaderAdminDisplayType, "Content:5"),
        };

        var contentTypeDefinition = await GetContactNavigationContentTypeAsync(contentItem);

        if (contentTypeDefinition is not null)
        {
            results.Add(Initialize<ContactNavigationAdminShapeViewModel>("ContactNavigationAdmin", model =>
            {
                model.ContactContentItem = contentItem;
                model.Definition = contentTypeDefinition;
                model.ShowEdit = true;
            }).Location(OrchardCoreConstants.DisplayType.DetailAdmin, "Content:1.5"));
        }

        return Combine(results);
    }

    public override async Task<IDisplayResult> EditAsync(ContentItem contentItem, BuildEditorContext context)
    {
        var contentTypeDefinition = await GetContactNavigationContentTypeAsync(contentItem);

        if (contentTypeDefinition is null)
        {
            return null;
        }

        return Initialize<ContactNavigationAdminShapeViewModel>("ContactNavigationAdmin", model =>
        {
            model.ContactContentItem = contentItem;
            model.Definition = contentTypeDefinition;
            model.ShowEdit = false;
        }).Location("Content:1.5")
        .RenderWhen(() => Task.FromResult(!context.IsNew));
    }

    /// <summary>
    /// Returns the contact's content type definition when this driver should render the contact navigation bar,
    /// or <see langword="null"/> when it should not. A contact type that also has the <c>ListPart</c> attached is
    /// skipped because the list part already renders its own navigation bar, and the activity buttons are merged
    /// into that bar through the <c>ListPartNavigationAdmin__OmnichannelContact</c> alternate instead of
    /// rendering a second bar.
    /// </summary>
    internal async Task<ContentTypeDefinition> GetContactNavigationContentTypeAsync(ContentItem contentItem)
    {
        if (string.IsNullOrEmpty(contentItem.ContentType))
        {
            return null;
        }

        if (_evaluatedContentTypes.TryGetValue(contentItem.ContentType, out _))
        {
            return _contactWithHeaderContentTypes.GetValueOrDefault(contentItem.ContentType);
        }

        var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

        _evaluatedContentTypes[contentItem.ContentType] = contentTypeDefinition;

        if (contentTypeDefinition is null ||
            !contentTypeDefinition.Parts.Any(x => x.Name == OmnichannelConstants.ContentParts.OmnichannelContact) ||
            contentTypeDefinition.Parts.Any(x => x.PartDefinition.Name == ContactListPartShapeTableProvider.ListPartName))
        {
            return null;
        }

        _contactWithHeaderContentTypes[contentItem.ContentType] = contentTypeDefinition;

        return contentTypeDefinition;
    }
}
