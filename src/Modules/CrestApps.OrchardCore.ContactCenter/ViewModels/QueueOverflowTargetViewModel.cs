namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// One hop of a queue's overflow chain as edited on the queue screen.
/// </summary>
public class QueueOverflowTargetViewModel
{
    public string QueueId { get; set; }

    public int AfterSeconds { get; set; }
}
