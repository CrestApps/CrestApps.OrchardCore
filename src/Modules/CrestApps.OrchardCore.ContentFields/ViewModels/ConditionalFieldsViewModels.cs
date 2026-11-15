using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.ContentFields.Conditions;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.ContentFields.ViewModels;

/// <summary>
/// The rules of an editor's fields, for the browser.
/// </summary>
public class ConditionalFieldsEditorViewModel
{
    /// <summary>
    /// Gets or sets the rules and sources, as JSON.
    /// </summary>
    public string Json { get; set; }
}

/// <summary>
/// The <b>Show this field when</b> section of a field's settings.
/// </summary>
public class ConditionalFieldSettingsViewModel
{
    /// <summary>
    /// Gets or sets whether all the conditions are needed, or any one.
    /// </summary>
    public ConditionMatch Match { get; set; }

    /// <summary>
    /// Gets or sets the condition rows; a row without a source is ignored.
    /// </summary>
    public List<ConditionalFieldConditionRow> Conditions { get; set; } = [];

    /// <summary>
    /// Gets or sets the sources a condition can read.
    /// </summary>
    [BindNever]
    public List<ContentConditionSourceDescriptor> Sources { get; set; } = [];

    /// <summary>
    /// Gets or sets the names of sources a saved condition reads that no longer exist.
    /// </summary>
    [BindNever]
    public List<string> MissingSources { get; set; } = [];
}

/// <summary>
/// One condition row of <see cref="ConditionalFieldSettingsViewModel"/>.
/// </summary>
public class ConditionalFieldConditionRow
{
    /// <summary>
    /// Gets or sets the source.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the operator.
    /// </summary>
    public ConditionOperator Operator { get; set; }

    /// <summary>
    /// Gets or sets the values chosen from the source's list.
    /// </summary>
    public List<string> Values { get; set; } = [];

    /// <summary>
    /// Gets or sets the values typed, one per line, for a source without a list.
    /// </summary>
    public string Text { get; set; }
}
