using System.ComponentModel.DataAnnotations;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// What callers hear while they wait, as edited on the queue screen.
/// </summary>
public class QueueTreatmentViewModel
{
    public string WelcomeMessage { get; set; }

    [Range(0, int.MaxValue)]
    public int AnnouncementIntervalSeconds { get; set; }

    public bool AnnouncePosition { get; set; }

    public bool AnnounceEstimatedWait { get; set; }

    public string HoldMusicMediaId { get; set; }

    public string CallbackDtmfKey { get; set; }

    [Range(0, int.MaxValue)]
    public int CallbackOfferAfterSeconds { get; set; }

    [Range(0, int.MaxValue)]
    public int MinimumEstimateSeconds { get; set; } = 30;

    [Range(0, int.MaxValue)]
    public int MaximumEstimateSeconds { get; set; } = 1_800;

    [Range(0, int.MaxValue)]
    public int AverageHandleTimeSeconds { get; set; }
}
