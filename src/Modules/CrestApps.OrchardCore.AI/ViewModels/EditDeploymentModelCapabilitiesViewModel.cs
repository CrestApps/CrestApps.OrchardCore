using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Backs the metadata-driven model capabilities editor rendered on the AI deployment editor. Operators
/// declare which registered features the underlying model supports and, per model parameter, whether the
/// deployment exposes it along with any narrowing of the registered allowed values, default, or range.
/// </summary>
public class EditDeploymentModelCapabilitiesViewModel
{
    /// <summary>
    /// Gets or sets the technical names of the features the operator declared the deployment supports.
    /// </summary>
    public string[] SelectedFeatures { get; set; } = [];

    /// <summary>
    /// Gets or sets the per-parameter metadata declared for the deployment.
    /// </summary>
    public List<DeploymentModelParameterViewModel> ModelParameters { get; set; } = [];

    /// <summary>
    /// Gets or sets every registered feature, used to render the feature checkboxes.
    /// </summary>
    [BindNever]
    public IReadOnlyList<AIDeploymentFeatureDescriptor> AvailableFeatures { get; set; } = [];
}

/// <summary>
/// Represents a single registered model parameter as declared for a deployment.
/// </summary>
public class DeploymentModelParameterViewModel
{
    /// <summary>
    /// Gets or sets the registered technical name of the parameter.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the deployment exposes this parameter.
    /// </summary>
    public bool IsSupported { get; set; }

    /// <summary>
    /// Gets or sets the subset of allowed values the deployment supports. When empty, every registered
    /// value is supported.
    /// </summary>
    public string[] SelectedAllowedValues { get; set; } = [];

    /// <summary>
    /// Gets or sets the value applied when an operator does not select one.
    /// </summary>
    public string DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets the inclusive minimum accepted value for numeric parameters.
    /// </summary>
    public double? Minimum { get; set; }

    /// <summary>
    /// Gets or sets the inclusive maximum accepted value for numeric parameters.
    /// </summary>
    public double? Maximum { get; set; }

    /// <summary>
    /// Gets or sets the increment applied by numeric editors.
    /// </summary>
    public double? Step { get; set; }

    /// <summary>
    /// Gets or sets the registered descriptor used to render the card. Never posted.
    /// </summary>
    [BindNever]
    public AIDeploymentParameterDescriptor Descriptor { get; set; }

    /// <summary>
    /// Gets a slug safe for use inside an element identifier.
    /// </summary>
    [BindNever]
    public string ElementId
        => Name?.Replace('.', '_');
}
