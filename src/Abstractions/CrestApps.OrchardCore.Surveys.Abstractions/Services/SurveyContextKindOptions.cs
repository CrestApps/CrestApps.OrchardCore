namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// The registered context kinds.
/// </summary>
public sealed class SurveyContextKindOptions
{
    /// <summary>
    /// Gets the context kinds by kind.
    /// </summary>
    public Dictionary<string, SurveyContextKindDescriptor> Kinds { get; } = new(StringComparer.OrdinalIgnoreCase);
}
