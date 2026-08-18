using System.Security.Claims;
using CrestApps.OrchardCore.Wizard.Models;
using CrestApps.OrchardCore.Wizard.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Contents;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Wizard.Drivers;

/// <summary>
/// Authors the step-definition content items of a <see cref="WizardPart"/> using the same contained-item
/// editor infrastructure as the Flows bag part, and renders the authored steps on the front end.
/// </summary>
public sealed class WizardPartDisplayDriver : ContentPartDisplayDriver<WizardPart>
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContentManager _contentManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger _logger;
    private readonly INotifier _notifier;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<IContentHandler> _contentHandlers;
    private readonly IEnumerable<IContentHandler> _reversedContentHandlers;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="WizardPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager used to create step items.</param>
    /// <param name="contentDefinitionManager">The definition manager used to resolve allowed step types.</param>
    /// <param name="serviceProvider">The service provider used to resolve display services lazily.</param>
    /// <param name="httpContextAccessor">The accessor used to read the current user.</param>
    /// <param name="logger">The logger used to report authoring warnings.</param>
    /// <param name="notifier">The notifier used to warn the user about invalid steps.</param>
    /// <param name="htmlLocalizer">The localizer used for notifier messages.</param>
    /// <param name="contentHandlers">The content handlers invoked while merging step items.</param>
    /// <param name="authorizationService">The service used to authorize step access.</param>
    public WizardPartDisplayDriver(
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        IServiceProvider serviceProvider,
        IHttpContextAccessor httpContextAccessor,
        ILogger<WizardPartDisplayDriver> logger,
        INotifier notifier,
        IHtmlLocalizer<WizardPartDisplayDriver> htmlLocalizer,
        IEnumerable<IContentHandler> contentHandlers,
        IAuthorizationService authorizationService)
    {
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _serviceProvider = serviceProvider;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _notifier = notifier;
        H = htmlLocalizer;
        _contentHandlers = contentHandlers;
        _reversedContentHandlers = contentHandlers.Reverse();
        _authorizationService = authorizationService;
    }

    /// <inheritdoc/>
    public override IDisplayResult Display(WizardPart part, BuildPartDisplayContext context)
    {
        var hasItems = part.Steps.Count > 0;

        return Initialize<WizardPartViewModel>(hasItems ? "WizardPart" : "WizardPart_Empty", m =>
        {
            m.WizardPart = part;
            m.BuildPartDisplayContext = context;
            m.Settings = context.TypePartDefinition.GetSettings<WizardPartSettings>();
        })
        .Location(OrchardCoreConstants.DisplayType.Detail, "Content")
        .Location(OrchardCoreConstants.DisplayType.Summary, "Content");
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(WizardPart part, BuildPartEditorContext context)
    {
        return Initialize<WizardPartEditViewModel>(GetEditorShapeType(context), async m =>
        {
            m.WizardPart = part;
            m.Updater = context.Updater;
            m.ContainedContentTypeDefinitions = await GetContainedContentTypesAsync(context.TypePartDefinition);
            m.AccessibleWidgets = await GetAccessibleWidgetsAsync(part.Steps);
            m.TypePartDefinition = context.TypePartDefinition;
        });
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(WizardPart part, UpdatePartEditorContext context)
    {
        var contentItemDisplayManager = _serviceProvider.GetRequiredService<IContentItemDisplayManager>();

        var model = new WizardPartEditViewModel { WizardPart = part };

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var contentItems = new Dictionary<string, ContentItem>();
        var existingContentItems = part.Steps.ToDictionary(x => x.ContentItemId, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < model.Prefixes.Length; i++)
        {
            var contentItem = await _contentManager.NewAsync(model.ContentTypes[i]);

            existingContentItems.TryGetValue(model.ContentItems[i], out var existingContentItem);

            var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

            if (existingContentItem == null && !await AuthorizeAsync(contentTypeDefinition, CommonPermissions.EditContent, contentItem))
            {
                continue;
            }

            if (existingContentItem != null)
            {
                if (!await AuthorizeAsync(contentTypeDefinition, CommonPermissions.EditContent, existingContentItem))
                {
                    contentItems.Add(existingContentItem.ContentItemId, existingContentItem);

                    continue;
                }

                var updateContentContext = new UpdateContentContext(contentItem);

                await _contentHandlers.InvokeAsync((handler, ctx) => handler.UpdatingAsync(ctx), updateContentContext, _logger);

                contentItem.ContentItemId = model.ContentItems[i];
                contentItem.Merge(existingContentItem);

                await contentItemDisplayManager.UpdateEditorAsync(contentItem, context.Updater, context.IsNew, htmlFieldPrefix: model.Prefixes[i]);
                await _reversedContentHandlers.InvokeAsync((handler, ctx) => handler.UpdatedAsync(ctx), updateContentContext, _logger);
            }
            else
            {
                var createContentContext = new CreateContentContext(contentItem);

                await _contentHandlers.InvokeAsync((handler, ctx) => handler.CreatingAsync(ctx), createContentContext, _logger);
                await contentItemDisplayManager.UpdateEditorAsync(contentItem, context.Updater, context.IsNew, htmlFieldPrefix: model.Prefixes[i]);
                await _reversedContentHandlers.InvokeAsync((handler, ctx) => handler.CreatedAsync(ctx), createContentContext, _logger);
            }

            contentItems.Add(contentItem.ContentItemId, contentItem);
        }

        foreach (var existingContentItem in part.Steps)
        {
            if (contentItems.ContainsKey(existingContentItem.ContentItemId))
            {
                continue;
            }

            var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(existingContentItem.ContentType);

            if (await AuthorizeAsync(contentTypeDefinition, CommonPermissions.DeleteContent, existingContentItem))
            {
                if (!model.ContentItems.Contains(existingContentItem.ContentItemId))
                {
                    continue;
                }
            }

            contentItems.Add(existingContentItem.ContentItemId, existingContentItem);
        }

        part.Steps = contentItems.Values.ToList();

        return Edit(part, context);
    }

    private async Task<IEnumerable<WizardPartWidgetViewModel>> GetAccessibleWidgetsAsync(IEnumerable<ContentItem> contentItems)
    {
        var widgets = new List<WizardPartWidgetViewModel>();

        foreach (var contentItem in contentItems)
        {
            var widget = new WizardPartWidgetViewModel
            {
                ContentItem = contentItem,
                Viewable = true,
                Editable = true,
                Deletable = true,
            };

            var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

            if (contentTypeDefinition == null)
            {
                _logger.LogWarning("The wizard step content item with id {ContentItemId} has no matching {ContentType} content type definition.", contentItem.ContentItemId, contentItem.ContentType);

                await _notifier.WarningAsync(H["The wizard step content item with id {0} has no matching {1} content type definition.", contentItem.ContentItemId, contentItem.ContentType]);

                continue;
            }

            widget.Viewable = await AuthorizeAsync(contentTypeDefinition, CommonPermissions.ViewContent, contentItem);
            widget.Editable = await AuthorizeAsync(contentTypeDefinition, CommonPermissions.EditContent, contentItem);
            widget.Deletable = await AuthorizeAsync(contentTypeDefinition, CommonPermissions.DeleteContent, contentItem);
            widget.ContentTypeDefinition = contentTypeDefinition;

            if (widget.Editable || widget.Viewable)
            {
                widgets.Add(widget);
            }
        }

        return widgets;
    }

    private async Task<bool> AuthorizeAsync(ContentTypeDefinition contentTypeDefinition, Permission permission, ContentItem contentItem)
    {
        if (contentTypeDefinition is not null && contentTypeDefinition.IsSecurable())
        {
            return await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, permission, contentItem);
        }

        return true;
    }

    private async Task<IEnumerable<ContentTypeDefinition>> GetContainedContentTypesAsync(ContentTypePartDefinition typePartDefinition)
    {
        var settings = typePartDefinition.GetSettings<WizardPartSettings>();
        var contentTypes = Enumerable.Empty<ContentTypeDefinition>();

        if (settings.ContainedStereotypes != null && settings.ContainedStereotypes.Length > 0)
        {
            contentTypes = (await _contentDefinitionManager.ListTypeDefinitionsAsync())
                .Where(contentType => contentType.HasStereotype() && settings.ContainedStereotypes.Contains(contentType.GetStereotype(), StringComparer.OrdinalIgnoreCase));
        }
        else if (settings.ContainedContentTypes != null && settings.ContainedContentTypes.Length > 0)
        {
            var definitions = new List<ContentTypeDefinition>();

            foreach (var contentType in settings.ContainedContentTypes)
            {
                var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);

                if (definition == null)
                {
                    continue;
                }

                definitions.Add(definition);
            }

            contentTypes = definitions;
        }

        var user = _httpContextAccessor.HttpContext.User;

        var accessibleContentTypes = new List<ContentTypeDefinition>();

        foreach (var contentType in contentTypes)
        {
            if (contentType.IsSecurable() && !await _authorizationService.AuthorizeContentTypeAsync(user, CommonPermissions.EditContent, contentType, GetCurrentOwner()))
            {
                continue;
            }

            accessibleContentTypes.Add(contentType);
        }

        return accessibleContentTypes;
    }

    private string GetCurrentOwner()
        => _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
}
