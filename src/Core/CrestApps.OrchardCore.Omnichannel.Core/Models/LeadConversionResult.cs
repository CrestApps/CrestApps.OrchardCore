using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// The outcome of converting a lead.
/// </summary>
public sealed class LeadConversionResult
{
    /// <summary>
    /// Gets whether the lead was converted, or had already been converted.
    /// </summary>
    public bool Succeeded => Errors.Count == 0;

    /// <summary>
    /// Gets the reasons the lead could not be converted.
    /// </summary>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Gets or sets whether the lead had already been converted, in which case nothing was changed.
    /// </summary>
    public bool AlreadyConverted { get; set; }

    /// <summary>
    /// Gets or sets the converted lead.
    /// </summary>
    public ContentItem Lead { get; set; }

    /// <summary>
    /// Gets or sets the contact the lead became or was merged into.
    /// </summary>
    public ContentItem Contact { get; set; }

    /// <summary>
    /// Gets or sets whether the contact was created by the conversion rather than merged into.
    /// </summary>
    public bool ContactCreated { get; set; }

    /// <summary>
    /// Gets or sets the account the contact joined.
    /// </summary>
    public ContentItem Account { get; set; }

    /// <summary>
    /// Gets or sets whether the account was created by the conversion.
    /// </summary>
    public bool AccountCreated { get; set; }

    /// <summary>
    /// Gets or sets the opportunity the conversion created.
    /// </summary>
    public ContentItem Opportunity { get; set; }

    /// <summary>
    /// Gets or sets the number of activities moved to the contact.
    /// </summary>
    public int MovedActivities { get; set; }

    /// <summary>
    /// Gets or sets the number of open activities cancelled.
    /// </summary>
    public int CancelledActivities { get; set; }
}
