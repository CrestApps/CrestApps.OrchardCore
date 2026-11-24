using CrestApps.OrchardCore.Learning.Lti.Models;

namespace CrestApps.OrchardCore.Learning.Lti.Core.DeepLinking;

/// <summary>
/// A tool's Deep Linking response, once checked.
/// </summary>
public sealed class LtiDeepLinkingResponse
{
    /// <summary>
    /// Gets or sets why the response was refused, or <see langword="null"/> when it was accepted.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets the tool that sent it.
    /// </summary>
    public ExternalTool Tool { get; set; }

    /// <summary>
    /// Gets or sets the request it answers.
    /// </summary>
    public LtiDeepLinkingData Data { get; set; }

    /// <summary>
    /// Gets the <c>ltiResourceLink</c> items to add to the course.
    /// </summary>
    public IList<LtiDeepLinkItem> Items { get; } = [];

    /// <summary>
    /// Gets the types of the content items that were not added, because the platform only accepts
    /// <c>ltiResourceLink</c>.
    /// </summary>
    public IList<string> RefusedTypes { get; } = [];

    /// <summary>
    /// Gets or sets the tool's message for the person (<c>msg</c>), or <see langword="null"/>.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the tool's error message for the person (<c>errormsg</c>), or <see langword="null"/>.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Gets whether the response was accepted.
    /// </summary>
    public bool Succeeded => string.IsNullOrEmpty(Error);
}
