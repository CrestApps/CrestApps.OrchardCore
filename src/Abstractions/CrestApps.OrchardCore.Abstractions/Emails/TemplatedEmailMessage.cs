namespace CrestApps.OrchardCore.Emails;

/// <summary>
/// The email to send, after the site's template of it.
/// </summary>
/// <param name="Subject">The subject.</param>
/// <param name="Text">The text body.</param>
/// <param name="Html">The HTML body.</param>
public sealed record TemplatedEmailMessage(string Subject, string Text, string Html);
