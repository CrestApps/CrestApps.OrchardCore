namespace CrestApps.OrchardCore.Learning.Credentials.Models;

/// <summary>
/// An Open Badges 3.0 alignment: a node of an outside framework, such as a competency, that the achievement meets.
/// </summary>
public sealed class CredentialAlignment
{
    /// <summary>
    /// Gets or sets the name of the node.
    /// </summary>
    public string TargetName { get; set; }

    /// <summary>
    /// Gets or sets the address of the node.
    /// </summary>
    public string TargetUrl { get; set; }

    /// <summary>
    /// Gets or sets a short description of the node.
    /// </summary>
    public string TargetDescription { get; set; }

    /// <summary>
    /// Gets or sets the name of the framework.
    /// </summary>
    public string TargetFramework { get; set; }

    /// <summary>
    /// Gets or sets the code of the node in its framework.
    /// </summary>
    public string TargetCode { get; set; }

    /// <summary>
    /// Gets or sets the Open Badges 3.0 alignment target type, such as <c>ceasn:Competency</c>.
    /// </summary>
    public string TargetType { get; set; }
}
