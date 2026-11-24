namespace CrestApps.OrchardCore.Learning.Lti.Core.DeepLinking;

/// <summary>
/// An <c>ltiResourceLink</c> content item of a Deep Linking response (Deep Linking 2.0, 3.3), as the platform keeps it.
/// </summary>
public sealed class LtiDeepLinkItem
{
    /// <summary>
    /// Gets or sets the title, or <see langword="null"/>.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the plain text description, or <see langword="null"/>.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the launch URL, or <see langword="null"/> for the tool's default target link URI.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the custom parameters, one <c>name=value</c> per line.
    /// </summary>
    public string CustomParameters { get; set; }

    /// <summary>
    /// Gets or sets the maximum score of the line item the tool asked for, or <see langword="null"/> without one.
    /// </summary>
    public decimal? ScoreMaximum { get; set; }

    /// <summary>
    /// Gets or sets the label of the line item.
    /// </summary>
    public string LineItemLabel { get; set; }

    /// <summary>
    /// Gets or sets the tool's resource id of the line item.
    /// </summary>
    public string LineItemResourceId { get; set; }

    /// <summary>
    /// Gets or sets the tool's tag of the line item.
    /// </summary>
    public string LineItemTag { get; set; }

    /// <summary>
    /// Gets or sets the content item as the tool sent it.
    /// </summary>
    public string Json { get; set; }
}
