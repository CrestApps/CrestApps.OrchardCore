namespace CrestApps.OrchardCore.Approvals.Models;

/// <summary>
/// The model of an approver rule value editor in the policy editor: the <c>ApproverRuleValue_Edit</c> shape's
/// <c>Editor</c> property, with the alternate <c>ApproverRuleValue_Edit__{Kind}</c> a module adds for its own approver
/// rule provider. The editor posts its value under <see cref="FieldName"/>.
/// </summary>
public class ApproverRuleValueEditorViewModel
{
    /// <summary>
    /// Gets or sets the provider's name.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the form field name the editor posts its value under.
    /// </summary>
    public string FieldName { get; set; }

    /// <summary>
    /// Gets or sets the element id prefix.
    /// </summary>
    public string FieldId { get; set; }

    /// <summary>
    /// Gets or sets the rule's value when its kind is this provider's, or <see langword="null"/>.
    /// </summary>
    public string Value { get; set; }
}
