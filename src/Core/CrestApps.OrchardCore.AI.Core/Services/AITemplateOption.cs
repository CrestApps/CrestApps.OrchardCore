namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// A prompt template offered as a choice, such as a speaking style or a guardrail topic.
/// </summary>
/// <param name="Id">The template identifier.</param>
/// <param name="Title">The title.</param>
/// <param name="Description">The description.</param>
public sealed record AITemplateOption(string Id, string Title, string Description);
