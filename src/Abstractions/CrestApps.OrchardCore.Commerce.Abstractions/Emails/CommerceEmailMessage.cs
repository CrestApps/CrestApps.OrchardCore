namespace CrestApps.OrchardCore.Commerce.Emails;

/// <summary>
/// The email to send, after the store's template of it.
/// </summary>
/// <param name="Subject">The subject.</param>
/// <param name="Text">The text body.</param>
/// <param name="Html">The HTML body.</param>
public sealed record CommerceEmailMessage(string Subject, string Text, string Html);
