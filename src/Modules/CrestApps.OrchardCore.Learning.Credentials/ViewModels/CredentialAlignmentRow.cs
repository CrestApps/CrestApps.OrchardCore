namespace CrestApps.OrchardCore.Learning.Credentials.ViewModels;

/// <summary>
/// One alignment in the definition editor.
/// </summary>
public class CredentialAlignmentRow
{
    /// <summary>
    /// Gets or sets the name of the framework node.
    /// </summary>
    public string TargetName { get; set; }

    /// <summary>
    /// Gets or sets the address of the node.
    /// </summary>
    public string TargetUrl { get; set; }

    /// <summary>
    /// Gets or sets the framework.
    /// </summary>
    public string TargetFramework { get; set; }

    /// <summary>
    /// Gets or sets the code of the node.
    /// </summary>
    public string TargetCode { get; set; }

    /// <summary>
    /// Gets or sets the alignment target type.
    /// </summary>
    public string TargetType { get; set; }

    /// <summary>
    /// Gets whether every field is empty.
    /// </summary>
    public bool IsEmpty
        => string.IsNullOrWhiteSpace(TargetName) &&
        string.IsNullOrWhiteSpace(TargetUrl) &&
        string.IsNullOrWhiteSpace(TargetFramework) &&
        string.IsNullOrWhiteSpace(TargetCode) &&
        string.IsNullOrWhiteSpace(TargetType);
}
