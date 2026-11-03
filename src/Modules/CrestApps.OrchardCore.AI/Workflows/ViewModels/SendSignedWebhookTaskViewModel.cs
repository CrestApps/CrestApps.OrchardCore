using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Workflows.ViewModels;

/// <summary>
/// Edits the Send Signed Webhook task.
/// </summary>
public class SendSignedWebhookTaskViewModel
{
    /// <summary>
    /// Gets or sets the Liquid expression for the address to post to.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the Liquid expression for the event name.
    /// </summary>
    public string EventName { get; set; }

    /// <summary>
    /// Gets or sets the Liquid template of the JSON body.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets a new signing secret, or empty to keep the saved one.
    /// </summary>
    public string Secret { get; set; }

    /// <summary>
    /// Gets or sets how many times a delivery is tried.
    /// </summary>
    public int MaxAttempts { get; set; }

    /// <summary>
    /// Gets or sets whether a secret is saved.
    /// </summary>
    [BindNever]
    public bool HasSecret { get; set; }
}
