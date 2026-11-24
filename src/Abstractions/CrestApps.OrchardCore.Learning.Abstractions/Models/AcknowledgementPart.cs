using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// The statement a learner confirms on an acknowledgement, such as "I have read and understood the policy". The
/// confirmation stores the statement and the content version as they were shown.
/// </summary>
public sealed class AcknowledgementPart : ContentPart
{
    /// <summary>
    /// Gets or sets the statement the learner confirms.
    /// </summary>
    public string Statement { get; set; }
}
