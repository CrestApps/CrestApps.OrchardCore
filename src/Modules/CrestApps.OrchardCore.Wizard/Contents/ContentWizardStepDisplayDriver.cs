using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Wizard.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Json;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Wizard.Contents;

/// <summary>
/// Displays and updates the per-session response content item for a content-driven wizard step. The step key
/// is recognized by the <see cref="WizardConstants.ContentStepPrefix"/> prefix, and the response item is
/// seeded from the authored step template on first render.
/// </summary>
public sealed class ContentWizardStepDisplayDriver : DisplayDriver<WizardFlow>
{
    private readonly IContentItemDisplayManager _contentItemDisplayManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISiteService _siteService;
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentWizardStepDisplayDriver"/> class.
    /// </summary>
    /// <param name="contentItemDisplayManager">The display manager used to build and update content item editors.</param>
    /// <param name="httpContextAccessor">The accessor used to read the current user from the HTTP context.</param>
    /// <param name="siteService">The site service used to resolve fallback owner settings.</param>
    /// <param name="documentJsonSerializerOptions">The JSON serializer options used to read saved step data.</param>
    /// <param name="serviceProvider">The service provider used to resolve content services lazily.</param>
    public ContentWizardStepDisplayDriver(
        IContentItemDisplayManager contentItemDisplayManager,
        IHttpContextAccessor httpContextAccessor,
        ISiteService siteService,
        IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions,
        IServiceProvider serviceProvider)
    {
        _contentItemDisplayManager = contentItemDisplayManager;
        _httpContextAccessor = httpContextAccessor;
        _siteService = siteService;
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(WizardFlow flow, BuildEditorContext context)
    {
        var step = flow.GetCurrentStep();

        if (!IsContentStep(flow, step, out var contentType))
        {
            return null;
        }

        return Factory("WizardFlowContentItem", async _ =>
        {
            if (!await IsStepTypeAllowedAsync(flow, contentType))
            {
                return null;
            }

            var contentItem = await GetOrCreateContentItemAsync(flow, step, contentType);

            return await _contentItemDisplayManager.BuildEditorAsync(contentItem, context.Updater, true, groupId: string.Empty, htmlFieldPrefix: step.Key);
        }).Location("Content");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(WizardFlow flow, UpdateEditorContext context)
    {
        var step = flow.GetCurrentStep();

        if (!IsContentStep(flow, step, out var contentType))
        {
            return null;
        }

        if (!await IsStepTypeAllowedAsync(flow, contentType))
        {
            return null;
        }

        var contentItem = await GetOrCreateContentItemAsync(flow, step, contentType);

        await _contentItemDisplayManager.UpdateEditorAsync(contentItem, context.Updater, true, groupId: string.Empty, htmlFieldPrefix: step.Key);

        var stepInfo = new ContentStep();

        stepInfo.ContentItems.Add(contentItem);

        flow.Session.SavedSteps[step.Key] = JObject.FromObject(stepInfo);

        step.State = WizardStepState.Completed;

        return Edit(flow, context);
    }

    private async Task<bool> IsStepTypeAllowedAsync(WizardFlow flow, string contentType)
    {
        if (flow.Session is not WizardSession session || string.IsNullOrEmpty(session.DefinitionId))
        {
            return false;
        }

        var contentManager = _serviceProvider.GetRequiredService<IContentManager>();

        var host = await contentManager.GetAsync(session.DefinitionId);

        if (host == null)
        {
            return false;
        }

        var contentDefinitionManager = _serviceProvider.GetRequiredService<IContentDefinitionManager>();

        var typeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(host.ContentType);

        var settings = typeDefinition?.Parts
            .FirstOrDefault(part => string.Equals(part.PartDefinition.Name, nameof(WizardPart), StringComparison.Ordinal))?
            .GetSettings<WizardPartSettings>();

        if (settings == null)
        {
            return false;
        }

        if (settings.ContainedStereotypes != null && settings.ContainedStereotypes.Length > 0)
        {
            var stepTypeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(contentType);

            return stepTypeDefinition != null &&
                stepTypeDefinition.HasStereotype() &&
                settings.ContainedStereotypes.Contains(stepTypeDefinition.GetStereotype(), StringComparer.OrdinalIgnoreCase);
        }

        if (settings.ContainedContentTypes != null && settings.ContainedContentTypes.Length > 0)
        {
            return settings.ContainedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool IsContentStep(WizardFlow flow, WizardStep step, out string contentType)
    {
        contentType = null;

        if (step == null ||
            flow.Session.CurrentStep == null ||
            !flow.Session.CurrentStep.StartsWith(WizardConstants.ContentStepPrefix, StringComparison.Ordinal) ||
            !step.Data.TryGetValue(WizardConstants.ContentTypeDataKey, out var value))
        {
            return false;
        }

        contentType = value?.ToString();

        return !string.IsNullOrEmpty(contentType);
    }

    private async Task<ContentItem> GetOrCreateContentItemAsync(WizardFlow flow, WizardStep step, string contentType)
    {
        ContentItem contentItem = null;

        if (flow.Session.SavedSteps.TryGetPropertyValue(step.Key, out var node))
        {
            var stepInfo = node.Deserialize<ContentStep>(_documentJsonSerializerOptions.SerializerOptions);

            contentItem = stepInfo?.ContentItems.FirstOrDefault();
        }

        if (contentItem == null)
        {
            // Lazily load content manager to avoid a possible circular reference.
            var contentManager = _serviceProvider.GetRequiredService<IContentManager>();

            contentItem = await contentManager.NewAsync(contentType);

            if (step.Data.TryGetValue(WizardConstants.StepTemplateDataKey, out var template) &&
                template is string templateJson &&
                !string.IsNullOrEmpty(templateJson))
            {
                var templateContent = JsonNode.Parse(templateJson) as JsonObject;

                if (templateContent != null)
                {
                    contentItem.Merge(templateContent);
                }
            }

            // When there is no logged in user, fall back to the super user as the owner for this content.
            contentItem.Owner = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? (await _siteService.GetSiteSettingsAsync()).SuperUser;
        }

        return contentItem;
    }
}
