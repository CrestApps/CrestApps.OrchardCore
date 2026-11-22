namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// A role that can use an AI profile and a tool of the profile it may not use.
/// </summary>
/// <param name="Role">The role; <c>Anonymous</c> for visitors who aren't signed in.</param>
/// <param name="ToolName">The tool.</param>
/// <param name="Permission">The permission the tool needs and the role lacks.</param>
public sealed record AIToolExposureIssue(string Role, string ToolName, string Permission);
