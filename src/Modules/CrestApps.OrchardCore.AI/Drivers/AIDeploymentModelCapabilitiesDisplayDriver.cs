using CrestApps.Core;
using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
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

    public AIDeploymentModelCapabilitiesDisplayDriver(IAIDeploymentCapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
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
            var hasMetadata = deployment.TryGet<AIDeploymentMetadata>(out var metadata);
            var selectedFeatures = ResolveSelectedFeatures(hasMetadata, metadata, registeredFeatures);

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

    /// <summary>
    /// Chooses which features the editor shows as declared for a deployment.
    /// </summary>
    /// <param name="hasMetadata">Whether the deployment has ever had capability metadata stored on it.</param>
    /// <param name="metadata">The stored metadata, or <see langword="null"/> when there is none.</param>
    /// <param name="registeredFeatures">Every model feature the application registers.</param>
    /// <remarks>
    /// A deployment that has never declared capabilities is unconstrained at runtime, because enforcement
    /// keys off the presence of the metadata rather than its contents. Saving this editor always writes
    /// metadata, so showing an empty set for one would turn "unconstrained" into "declares nothing" the
    /// first time an operator opened an older deployment to change something unrelated -- quietly costing
    /// it streaming and tool calling. It is offered the defaults instead, which is how it already behaves.
    /// <para>
    /// Metadata that exists but lists no feature is left empty: that is an operator who cleared every box,
    /// and their choice is not ours to undo.
    /// </para>
    /// </remarks>
    internal static HashSet<string> ResolveSelectedFeatures(
        bool hasMetadata,
        AIDeploymentMetadata metadata,
        IReadOnlyList<AIDeploymentFeatureDescriptor> registeredFeatures)
    {
        if (metadata?.Features is { Length: > 0 })
        {
            return new HashSet<string>(metadata.Features, StringComparer.OrdinalIgnoreCase);
        }

        if (hasMetadata)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return new HashSet<string>(
            registeredFeatures.Where(feature => feature.EnabledByDefault).Select(feature => feature.Name),
            StringComparer.OrdinalIgnoreCase);
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
