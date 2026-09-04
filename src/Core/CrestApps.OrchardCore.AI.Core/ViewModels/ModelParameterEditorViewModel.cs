using System.Text.Json;
using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Core.ViewModels;

/// <summary>
/// Backs the metadata-driven model parameter editor rendered by the <c>AIModelParameters_Edit</c> shape on
/// the AI profile, profile template, and chat interaction editors. Only the parameters exposed by the
/// selected deployment are rendered and posted; every other registered parameter is hidden.
/// </summary>
public sealed class ModelParameterEditorViewModel
{
    /// <summary>
    /// Gets or sets the selected parameter values keyed by their registered technical name. This is the
    /// only member posted back by the editor.
    /// </summary>
    public Dictionary<string, string> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the name of the form field that holds the selected chat deployment.
    /// </summary>
    [BindNever]
    public string DeploymentFieldName { get; set; } = "ChatDeploymentName";

    /// <summary>
    /// Gets or sets the prefix applied to generated element identifiers so a page can render
    /// more than one editor without colliding.
    /// </summary>
    [BindNever]
    public string ElementPrefix { get; set; } = "modelParameters";

    /// <summary>
    /// Gets or sets every registered model parameter along with the value currently selected.
    /// </summary>
    [BindNever]
    public List<ModelParameterFieldViewModel> Parameters { get; set; } = [];

    /// <summary>
    /// Gets or sets the per-deployment capability map serialized as JSON and consumed by the editor script.
    /// </summary>
    [BindNever]
    public string CapabilitiesJson { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the per-deployment trained feature map serialized as JSON and consumed by the editor
    /// script to render the read-only capability badges.
    /// </summary>
    [BindNever]
    public string FeaturesJson { get; set; } = "{}";

    /// <summary>
    /// Gets a value indicating whether at least one parameter is registered.
    /// </summary>
    [BindNever]
    public bool HasParameters
        => Parameters.Count > 0;
}

/// <summary>
/// Represents a single registered model parameter rendered by the editor.
/// </summary>
public sealed class ModelParameterFieldViewModel
{
    public string Name { get; set; }

    public string DisplayName { get; set; }

    public string Description { get; set; }

    public AIDeploymentParameterKind Kind { get; set; }

    public List<ModelParameterOptionViewModel> AllowedValues { get; set; } = [];

    public string Value { get; set; }

    public string ElementId
        => Name?.Replace('.', '_');
}

/// <summary>
/// Represents a selectable value of a choice parameter.
/// </summary>
public sealed class ModelParameterOptionViewModel
{
    public string Value { get; set; }

    public string DisplayName { get; set; }
}

/// <summary>
/// Describes the effective metadata of a single parameter for one deployment. The shape of this type
/// matches the JSON consumed by the editor script.
/// </summary>
public sealed class ModelParameterCapabilityViewModel
{
    public string[] AllowedValues { get; set; }

    public string DefaultValue { get; set; }

    public double? Minimum { get; set; }

    public double? Maximum { get; set; }

    public double? Step { get; set; }

    public static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web);
}
