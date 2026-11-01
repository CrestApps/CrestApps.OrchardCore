namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// A parameter of a labor rule.
/// </summary>
/// <param name="Name">The parameter's name, stored in the rule's parameters.</param>
/// <param name="Label">What it is called on screen.</param>
/// <param name="Kind">What it holds.</param>
/// <param name="DefaultValue">The value a new rule starts with, written invariantly.</param>
public sealed record LaborRuleParameter(string Name, string Label, LaborRuleParameterKind Kind, string DefaultValue);
