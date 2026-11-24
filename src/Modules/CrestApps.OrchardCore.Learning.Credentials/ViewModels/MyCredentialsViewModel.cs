namespace CrestApps.OrchardCore.Learning.Credentials.ViewModels;

/// <summary>
/// The My credentials page.
/// </summary>
public class MyCredentialsViewModel
{
    /// <summary>
    /// Gets or sets the learner's awards, newest first.
    /// </summary>
    public IList<MyCredentialEntry> Entries { get; set; } = [];
}
