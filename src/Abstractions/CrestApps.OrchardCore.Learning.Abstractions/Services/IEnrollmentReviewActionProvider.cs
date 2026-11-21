using CrestApps.OrchardCore.Learning.Models;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Offers actions on an enrollment flagged for review, shown on its review card, such as revoking a certificate the
/// completion earned.
/// </summary>
public interface IEnrollmentReviewActionProvider
{
    /// <summary>
    /// Lists the actions for an enrollment under review.
    /// </summary>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The actions.</returns>
    Task<IEnumerable<EnrollmentReviewAction>> GetActionsAsync(Enrollment enrollment, CancellationToken cancellationToken = default);
}

/// <summary>
/// An action on an enrollment under review: a button that posts to a URL.
/// </summary>
public sealed class EnrollmentReviewAction
{
    /// <summary>
    /// Gets or sets the button text.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets a line that explains what the action does.
    /// </summary>
    public string Hint { get; set; }

    /// <summary>
    /// Gets or sets the URL the button posts to.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the question asked before posting.
    /// </summary>
    public string Confirmation { get; set; }
}
