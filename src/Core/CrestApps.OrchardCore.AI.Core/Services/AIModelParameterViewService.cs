using System.Text.Json;
using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core.ViewModels;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Builds the metadata-driven model parameter editor from the registered parameter definitions and
/// the metadata declared by each AI deployment. Shared by the AI profile, profile template, and chat
/// interaction editors.
/// </summary>
public sealed class AIModelParameterViewService
{
    private readonly IAIDeploymentCapabilityService _capabilityService;
    private readonly INamedSourceCatalog<AIDeployment> _deploymentCatalog;

    public AIModelParameterViewService(
        IAIDeploymentCapabilityService capabilityService,
        INamedSourceCatalog<AIDeployment> deploymentCatalog)
    {
        _capabilityService = capabilityService;
        _deploymentCatalog = deploymentCatalog;
    }

    /// <summary>
    /// Builds the editor model for the given selected values.
    /// </summary>
    /// <param name="values">The values currently selected, keyed by parameter technical name.</param>
    /// <param name="deploymentFieldName">The name of the form field that holds the selected chat deployment.</param>
    /// <param name="elementPrefix">The prefix applied to generated element identifiers.</param>
    public async Task<ModelParameterEditorViewModel> BuildAsync(
        IReadOnlyDictionary<string, string> values,
        string deploymentFieldName = "ChatDeploymentName",
        string elementPrefix = "modelParameters",
        string bindingPrefix = null)
    {
        var model = new ModelParameterEditorViewModel
        {
            DeploymentFieldName = deploymentFieldName,
            ElementPrefix = elementPrefix,
            BindingPrefix = bindingPrefix,
        };

        foreach (var descriptor in _capabilityService.GetRegisteredParameters())
        {
            model.Parameters.Add(new ModelParameterFieldViewModel
            {
                Name = descriptor.Name,
                DisplayName = descriptor.DisplayName?.Value ?? descriptor.Name,
                Description = descriptor.Description?.Value,
                Kind = descriptor.Kind,
                Value = values is not null && values.TryGetValue(descriptor.Name, out var value) ? value : null,
                AllowedValues = [.. descriptor.AllowedValues.Select(option => new ModelParameterOptionViewModel
                {
                    Value = option.Value,
                    DisplayName = option.DisplayName?.Value ?? option.Value,
                })],
            });
        }

        var deployments = await _deploymentCatalog.GetAllAsync();

        model.CapabilitiesJson = JsonSerializer.Serialize(BuildCapabilityMap(deployments), ModelParameterCapabilityViewModel.SerializerOptions);
        model.FeaturesJson = JsonSerializer.Serialize(BuildFeatureMap(deployments), ModelParameterCapabilityViewModel.SerializerOptions);

        return model;
    }

    private Dictionary<string, string[]> BuildFeatureMap(IEnumerable<AIDeployment> deployments)
    {
        var map = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var deployment in deployments)
        {
            if (string.IsNullOrWhiteSpace(deployment.Name))
            {
                continue;
            }

            var capabilities = _capabilityService.GetCapabilities(deployment);

            if (capabilities.Features.Count == 0)
            {
                continue;
            }

            map[deployment.Name] = [.. capabilities.Features.Select(feature => feature.DisplayName?.Value ?? feature.Name)];
        }

        return map;
    }

    private Dictionary<string, Dictionary<string, ModelParameterCapabilityViewModel>> BuildCapabilityMap(IEnumerable<AIDeployment> deployments)
    {
        var map = new Dictionary<string, Dictionary<string, ModelParameterCapabilityViewModel>>(StringComparer.OrdinalIgnoreCase);

        foreach (var deployment in deployments)
        {
            if (string.IsNullOrWhiteSpace(deployment.Name))
            {
                continue;
            }

            var capabilities = _capabilityService.GetCapabilities(deployment);

            if (capabilities.Parameters.Count == 0)
            {
                continue;
            }

            var entries = new Dictionary<string, ModelParameterCapabilityViewModel>(StringComparer.OrdinalIgnoreCase);

            foreach (var parameter in capabilities.Parameters)
            {
                entries[parameter.Name] = new ModelParameterCapabilityViewModel
                {
                    AllowedValues = parameter.AllowedValues is { Count: > 0 }
                        ? [.. parameter.AllowedValues.Select(option => option.Value)]
                        : null,
                    DefaultValue = parameter.DefaultValue,
                    Minimum = parameter.Minimum,
                    Maximum = parameter.Maximum,
                    Step = parameter.Step,
                };
            }

            map[deployment.Name] = entries;
        }

        return map;
    }
}
