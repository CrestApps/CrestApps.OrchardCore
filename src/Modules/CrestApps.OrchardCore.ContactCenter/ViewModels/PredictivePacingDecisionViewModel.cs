using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// The last over-dial decision of one campaign, as a dialer profile's editor shows it.
/// </summary>
public class PredictivePacingDecisionViewModel
{
    /// <summary>
    /// Gets or sets the campaign queue the decision paced.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the name of the campaign the queue belongs to.
    /// </summary>
    public string CampaignName { get; set; }

    /// <summary>
    /// Gets or sets when the cycle that decided ran.
    /// </summary>
    public DateTime? LastCycleUtc { get; set; }

    /// <summary>
    /// Gets or sets what the cycle measured and decided.
    /// </summary>
    public PredictivePacingSnapshot Decision { get; set; }
}
