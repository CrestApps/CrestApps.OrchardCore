namespace CrestApps.OrchardCore.Learning.Credentials.ViewModels;

/// <summary>
/// One award rule in the definition editor.
/// </summary>
public class CredentialRuleRow
{
    /// <summary>
    /// Gets or sets the rule as <c>kind|target</c>; empty for no rule.
    /// </summary>
    public string Value { get; set; }
}
