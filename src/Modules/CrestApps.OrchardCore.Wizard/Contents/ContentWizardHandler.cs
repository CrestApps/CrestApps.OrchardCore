using System.Text.Json;
using CrestApps.OrchardCore.Wizard.Handlers;
using CrestApps.OrchardCore.Wizard.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.Wizard.Contents;

/// <summary>
/// Projects a content item's <see cref="WizardPart"/> step-definition items into wizard steps when a
/// content-driven wizard session is activated, and persists the collected response content items when the
/// wizard completes according to the configured <see cref="WizardCompletionPolicy"/>. Each authored step
/// becomes a data-collecting wizard step whose response is a per-session content item cloned from the
/// authored default field values, so authored content is never mutated by a running wizard.
/// </summary>
public sealed class ContentWizardHandler : WizardHandlerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentWizardHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve content services lazily.</param>
    /// <param name="documentJsonSerializerOptions">The JSON serializer options used to read saved step data.</param>
    public ContentWizardHandler(
        IServiceProvider serviceProvider,
        IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions)
    {
        _serviceProvider = serviceProvider;
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
    }

    /// <inheritdoc/>
    public override async Task ActivatingAsync(WizardFlowActivatingContext context)
    {
        var session = context.Session;

        if (!IsContentWizard(session))
        {
            return;
        }

        // Lazily resolve the content manager to avoid a possible circular reference during activation.
        var contentManager = _serviceProvider.GetRequiredService<IContentManager>();

        var definition = await contentManager.GetAsync(session.DefinitionId);

        var wizardPart = definition?.Get<WizardPart>(nameof(WizardPart));

        if (wizardPart == null || wizardPart.Steps.Count == 0)
        {
            return;
        }

        var settings = await GetSettingsAsync(definition.ContentType);

        session.Properties[ContentWizardConstants.CompletionPolicyPropertyKey] = settings.CompletionPolicy.ToString();

        var order = 0;

        foreach (var stepItem in wizardPart.Steps)
        {
            var step = new WizardStep
            {
                Key = WizardConstants.ContentStepPrefix + IdGenerator.GenerateId(),
                Title = !string.IsNullOrEmpty(stepItem.DisplayText) ? stepItem.DisplayText : stepItem.ContentType,
                Order = order++,
                CollectData = true,
            };

            step.Data[WizardConstants.ContentTypeDataKey] = stepItem.ContentType;
            step.Data[WizardConstants.DefinitionContentItemIdDataKey] = stepItem.ContentItemId;
            step.Data[WizardConstants.StepTemplateDataKey] = stepItem.Content?.ToJsonString();

            session.Steps.Add(step);
        }
    }

    /// <inheritdoc/>
    public override async Task CompletingAsync(WizardFlowCompletingContext context)
    {
        if (context.Flow.Session is not WizardSession session || !IsContentWizard(session))
        {
            return;
        }

        var policy = GetCompletionPolicy(session);

        if (policy == WizardCompletionPolicy.None)
        {
            return;
        }

        var contentManager = _serviceProvider.GetRequiredService<IContentManager>();

        foreach (var savedStep in session.SavedSteps)
        {
            var stepInfo = savedStep.Value.Deserialize<ContentStep>(_documentJsonSerializerOptions.SerializerOptions);

            if (stepInfo == null)
            {
                continue;
            }

            foreach (var contentItem in stepInfo.ContentItems)
            {
                await contentManager.CreateAsync(contentItem, VersionOptions.Draft);

                if (policy == WizardCompletionPolicy.Publish)
                {
                    await contentManager.PublishAsync(contentItem);
                }
            }
        }
    }

    private static bool IsContentWizard(WizardSession session)
        => string.Equals(session.WizardType, ContentWizardConstants.WizardType, StringComparison.OrdinalIgnoreCase) &&
           !string.IsNullOrEmpty(session.DefinitionId);

    private static WizardCompletionPolicy GetCompletionPolicy(WizardSession session)
    {
        if (session.Properties.TryGetPropertyValue(ContentWizardConstants.CompletionPolicyPropertyKey, out var value) &&
            Enum.TryParse<WizardCompletionPolicy>(value?.GetValue<string>(), out var policy))
        {
            return policy;
        }

        return WizardCompletionPolicy.Publish;
    }

    private async Task<WizardPartSettings> GetSettingsAsync(string contentType)
    {
        var contentDefinitionManager = _serviceProvider.GetRequiredService<IContentDefinitionManager>();

        var typeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(contentType);

        var partDefinition = typeDefinition?.Parts.FirstOrDefault(part =>
            string.Equals(part.PartDefinition.Name, nameof(WizardPart), StringComparison.Ordinal));

        return partDefinition?.GetSettings<WizardPartSettings>() ?? new WizardPartSettings();
    }
}
