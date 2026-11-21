namespace CrestApps.OrchardCore.Learning.Credentials.Services;

/// <summary>
/// Something a rule may watch, such as a course.
/// </summary>
/// <param name="Id">The id stored on the rule.</param>
/// <param name="Title">The name shown in the editor.</param>
public sealed record CredentialRuleTarget(string Id, string Title);
