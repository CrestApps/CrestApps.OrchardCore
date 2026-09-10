using CrestApps.Core;
using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Renders the metadata-driven model capabilities editor on the AI deployment editor and persists the
/// declared features and per-parameter metadata onto <see cref="AIDeploymentMetadata"/>.
/// </summary>
internal sealed class AIDeploymentModelCapabilitiesDisplayDriver : DisplayDriver<AIDeployment>
{
    private readonly IAIDeploymentCapabilityService _capabilityService;

    internal readonly IStringLocalizer S;

    public AIDeploymentModelCapabilitiesDisplayDriver(
        IAIDeploymentCapabilityService capabilityService,
        IStringLocalizer<AIDeploymentModelCapabilitiesDisplayDriver> stringLocalizer)
    {
        _capabilityService = capabilityService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIDeployment deployment, BuildEditorContext context)
    {
        var registeredFeatures = _capabilityService.GetRegisteredFeatures();
        var registeredParameters = _capabilityService.GetRegisteredParameters();

        if (registeredFeatures.Count == 0 && registeredParameters.Count == 0)
        {
            return null;
        }

        return Initialize<EditDeploymentModelCapabilitiesViewModel>("AIDeploymentModelCapabilities_Edit", model =>
        {
            deployment.TryGet<AIDeploymentMetadata>(out var metadata);

            // A deployment that has never declared capabilities is offered the defaults, new or not. It is
            // unconstrained at run time, so the defaults are what it already behaves like -- and showing an
            // empty list instead used to save Features = [] the moment anything unrelated was edited,
            // quietly turning "unconstrained" into "declares nothing". Now that at least one capability is
            // required, that same empty list would simply block the save.
            var selectedFeatures = metadata?.Features is { Length: > 0 }
                ? new HashSet<string>(metadata.Features, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(registeredFeatures.Where(feature => feature.EnabledByDefault).Select(feature => feature.Name), StringComparer.OrdinalIgnoreCase);

            model.AvailableFeatures = registeredFeatures;
            model.SelectedFeatures = [.. selectedFeatures];

            model.ModelParameters = registeredParameters
                .Select(descriptor =>
                {
                    var stored = metadata is not null && metadata.Parameters.TryGetValue(descriptor.Name, out var value)
                        ? value
                        : null;

                    return new DeploymentModelParameterViewModel
                    {
                        Name = descriptor.Name,
                        Descriptor = descriptor,
                        IsSupported = stored is not null,
                        SelectedAllowedValues = stored?.AllowedValues ?? [],
                        DefaultValue = stored?.DefaultValue ?? descriptor.DefaultValue,
                        Minimum = stored?.Minimum ?? descriptor.Minimum,
                        Maximum = stored?.Maximum ?? descriptor.Maximum,
                        Step = stored?.Step ?? descriptor.Step,
                    };
                })
                .ToList();
        }).Location("Content:10");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDeployment deployment, UpdateEditorContext context)
    {
        var registeredFeatures = _capabilityService.GetRegisteredFeatures();
        var registeredParameters = _capabilityService.GetRegisteredParameters();

        if (registeredFeatures.Count == 0 && registeredParameters.Count == 0)
        {
            return null;
        }

        var model = new EditDeploymentModelCapabilitiesViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var registeredFeatureNames = new HashSet<string>(registeredFeatures.Select(feature => feature.Name), StringComparer.OrdinalIgnoreCase);
        var registeredParameterMap = registeredParameters.ToDictionary(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase);

        var metadata = new AIDeploymentMetadata
        {
            Features = (model.SelectedFeatures ?? [])
                .Where(feature => registeredFeatureNames.Contains(feature))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
        };

        // Capabilities are the only thing a deployment says about itself now that the purpose is gone, so a
        // deployment that declares none describes nothing and cannot be resolved for any slot.
        if (registeredFeatures.Count > 0 && metadata.Features.Length == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.SelectedFeatures), S["At least one model capability is required."]);
        }

        foreach (var parameter in model.ModelParameters ?? [])
        {
            if (!parameter.IsSupported ||
                string.IsNullOrWhiteSpace(parameter.Name) ||
                !registeredParameterMap.TryGetValue(parameter.Name, out var descriptor))
            {
                continue;
            }

            var allowedValues = parameter.SelectedAllowedValues?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            metadata.Parameters[descriptor.Name] = new AIDeploymentParameter
            {
                AllowedValues = allowedValues is { Length: > 0 } ? allowedValues : null,
                DefaultValue = string.IsNullOrWhiteSpace(parameter.DefaultValue) ? null : parameter.DefaultValue.Trim(),
                Minimum = descriptor.Kind is AIDeploymentParameterKind.Number or AIDeploymentParameterKind.Integer ? parameter.Minimum : null,
                Maximum = descriptor.Kind is AIDeploymentParameterKind.Number or AIDeploymentParameterKind.Integer ? parameter.Maximum : null,
                Step = descriptor.Kind is AIDeploymentParameterKind.Number or AIDeploymentParameterKind.Integer ? parameter.Step : null,
            };
        }

        deployment.Put(metadata);

        return Edit(deployment, context);
    }
}
