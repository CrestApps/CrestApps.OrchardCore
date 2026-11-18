namespace CrestApps.OrchardCore;

/// <summary>
/// The CrestApps AI feature ids that modules without a reference to <c>CrestApps.OrchardCore.AI.Core</c> require, such as
/// the startup that registers a module's AI tools, so a feature id is never repeated as a string literal. Code that
/// references <c>CrestApps.OrchardCore.AI.Core</c> uses <c>AIConstants.Feature</c> instead.
/// </summary>
public static class AIFeatureIds
{
    /// <summary>
    /// The AI feature, <c>CrestApps.OrchardCore.AI</c>, which AI tools need.
    /// </summary>
    public const string Area = "CrestApps.OrchardCore.AI";
}
