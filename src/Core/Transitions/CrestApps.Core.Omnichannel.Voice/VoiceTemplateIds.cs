namespace CrestApps.Core.Omnichannel.Voice;

/// <summary>
/// The prompt templates the automated voice module ships, by the id they are discovered under.
/// </summary>
/// <remarks>
/// A template's id is its file name without the extension, and the files live in <c>Templates/Prompts</c> in the
/// module so that discovery is feature-aware. Naming them here keeps the string that must match a file name in
/// one place rather than at each call site.
/// </remarks>
public static class VoiceTemplateIds
{
    /// <summary>
    /// Reviews a finished call and produces its summary and disposition.
    /// </summary>
    public const string ConclusionAnalysis = "voice-conclusion-analysis";
}
