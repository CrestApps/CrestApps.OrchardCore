using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.AI.Tooling.Instances;
using CrestApps.Core.AI.Tooling.Parameters;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Tools.Drivers;

/// <summary>
/// Renders the optional user-declared parameters editor on a tool instance, for sources that opt into
/// parameter support by declaring <see cref="AIToolInstanceParameterCapabilities"/>. Values are persisted
/// to <see cref="AIToolInstanceParametersMetadata"/> on the instance.
/// </summary>
internal sealed class AIToolInstanceParametersDisplayDriver : DisplayDriver<AIToolInstance>
{
    private readonly AIOptions _aiOptions;
    private readonly IEnumerable<IAIToolParameterContextResolver> _contextResolvers;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    public AIToolInstanceParametersDisplayDriver(
        IOptions<AIOptions> aiOptions,
        IEnumerable<IAIToolParameterContextResolver> contextResolvers,
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<AIToolInstanceParametersDisplayDriver> stringLocalizer)
    {
        _aiOptions = aiOptions.Value;
        _contextResolvers = contextResolvers;
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIToolInstance instance, BuildEditorContext context)
    {
        var capabilities = GetCapabilities(instance);

        if (capabilities is not { Supported: true })
        {
            return null;
        }

        return Initialize<EditToolInstanceParametersViewModel>("AIToolInstanceParameters_Edit", model =>
        {
            model.Parameters = AIToolInstanceParameterViewModel.FromParameters(AIToolParameterBinder.GetParameters(instance));
            model.ParameterCapabilities = new Dictionary<string, AIToolInstanceParameterCapabilities>(StringComparer.OrdinalIgnoreCase)
            {
                [instance.Source] = capabilities,
            };
            model.ContextKeys = [.. _contextResolvers.SelectMany(resolver => resolver.SupportedKeys)];
        }).Location("Content:10");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIToolInstance instance, UpdateEditorContext context)
    {
        var capabilities = GetCapabilities(instance);

        if (capabilities is not { Supported: true })
        {
            return null;
        }

        var model = new EditToolInstanceParametersViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var protector = _dataProtectionProvider.CreateProtector(HttpApiRequestToolConstants.DataProtectionPurpose);

        var parameters = AIToolInstanceParameterViewModel.ToParameters(
            model.Parameters,
            AIToolParameterBinder.GetParameters(instance),
            protector.Protect);

        foreach (var (index, error) in AIToolParameterValidator.Validate(parameters, capabilities))
        {
            var key = index >= 0
                ? $"{nameof(model.Parameters)}[{index}].{nameof(AIToolInstanceParameterViewModel.Name)}"
                : nameof(model.Parameters);

            context.Updater.ModelState.AddModelError(Prefix, key, error);
        }

        if (context.Updater.ModelState.IsValid)
        {
            instance.Put(new AIToolInstanceParametersMetadata { Parameters = parameters });
        }

        return Edit(instance, context);
    }

    private AIToolInstanceParameterCapabilities GetCapabilities(AIToolInstance instance)
    {
        if (!string.IsNullOrEmpty(instance.Source) &&
            _aiOptions.ToolInstanceSources.TryGetValue(instance.Source, out var entry))
        {
            return entry.Parameters;
        }

        return null;
    }
}
