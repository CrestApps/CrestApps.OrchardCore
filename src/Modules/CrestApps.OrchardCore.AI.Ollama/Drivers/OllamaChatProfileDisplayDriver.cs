using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Ollama.Models;
using CrestApps.OrchardCore.AI.Ollama.Services;
using CrestApps.OrchardCore.AI.Ollama.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Ollama.Drivers;

public sealed class OllamaChatProfileDisplayDriver : DisplayDriver<AIChatProfile>
{
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    public OllamaChatProfileDisplayDriver(
        IServiceProvider serviceProvider,
        IStringLocalizer<OllamaChatProfileDisplayDriver> stringLocalizer)
    {
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIChatProfile profile, BuildEditorContext context)
    {
        var profileSource = _serviceProvider.GetKeyedService<IAIChatProfileSource>(profile.Source);

        if (profileSource.TechnologyName != OllamaProfileSource.Key)
        {
            return null;
        }

        return Initialize<ChatProfileMetadataViewModel>("OllamaChatProfileMetadata_Edit", model =>
        {
            var metadata = profile.As<OllamaChatProfileMetadata>();

            model.SystemMessage = metadata.SystemMessage;
            model.FrequencyPenalty = metadata.FrequencyPenalty;
            model.PastMessagesCount = metadata.PastMessagesCount;
            model.PresencePenalty = metadata.PresencePenalty;
            model.Temperature = metadata.Temperature;
            model.MaxTokens = metadata.MaxTokens;
            model.TopP = metadata.TopP;

            model.IsSystemMessageLocked = profile.GetSettings<OllamaChatProfileSettings>().LockSystemMessage;

        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIChatProfile profile, UpdateEditorContext context)
    {
        var profileSource = _serviceProvider.GetKeyedService<IAIChatProfileSource>(profile.Source);

        if (profileSource.TechnologyName != OllamaProfileSource.Key)
        {
            return null;
        }

        var model = new ChatProfileMetadataViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var metadata = profile.As<OllamaChatProfileMetadata>();

        metadata.FrequencyPenalty = model.FrequencyPenalty;
        metadata.PastMessagesCount = model.PastMessagesCount;
        metadata.PresencePenalty = model.PresencePenalty;
        metadata.Temperature = model.Temperature;
        metadata.MaxTokens = model.MaxTokens;
        metadata.TopP = model.TopP;

        var settings = profile.GetSettings<OllamaChatProfileSettings>();

        if (!settings.LockSystemMessage)
        {
            metadata.SystemMessage = model.SystemMessage;
        }

        profile.Put(metadata);

        return Edit(profile, context);
    }
}
