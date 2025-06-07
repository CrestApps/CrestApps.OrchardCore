using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Tools.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Tools.Drivers;

internal sealed class InvokableToolMetadataDisplayDriver : DisplayDriver<AIToolInstance>
{
    internal readonly IStringLocalizer S;

    private readonly IServiceProvider _serviceProvider;

    public InvokableToolMetadataDisplayDriver(
        IStringLocalizer<AIProfileToolMetadataDisplayDriver> stringLocalizer,
        IServiceProvider serviceProvider)
    {
        S = stringLocalizer;
        _serviceProvider = serviceProvider;
    }

    public override IDisplayResult Edit(AIToolInstance instance, BuildEditorContext context)
    {
        var source = _serviceProvider.GetKeyedService<IAIToolSource>(instance.Source);

        if (source is null || source.Type != AIToolSourceType.Function)
        {
            return null;
        }

        return Initialize<InvokableToolViewModel>("InvokableToolMetadata_Edit", model =>
        {
            var metadata = instance.As<InvokableToolMetadata>();
            model.Description = metadata.Description;
        }).Location("Content:2");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIToolInstance instance, UpdateEditorContext context)
    {
        var source = _serviceProvider.GetKeyedService<IAIToolSource>(instance.Source);

        if (source is null || source.Type != AIToolSourceType.Function)
        {
            return null;
        }

        var model = new InvokableToolViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Description))
        {
            context.Updater.ModelState.AddModelError(nameof(model.Description), S["The Description is required."]);
        }

        instance.Put(new InvokableToolMetadata
        {
            Description = model.Description
        });

        return Edit(instance, context);
    }
}
