using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Takes part in converting a lead. <see cref="ConvertingAsync"/> runs once the contact, account and opportunity are
/// built but before anything is saved, so a handler can copy more of the lead onto them. <see cref="ConvertedAsync"/>
/// runs after the lead is closed as converted.
/// </summary>
public interface ILeadConversionHandler
{
    /// <summary>
    /// Runs before the converted records are saved.
    /// </summary>
    /// <param name="context">The conversion.</param>
    Task ConvertingAsync(LeadConversionContext context) => Task.CompletedTask;

    /// <summary>
    /// Runs after the lead was converted.
    /// </summary>
    /// <param name="context">The conversion.</param>
    Task ConvertedAsync(LeadConversionContext context) => Task.CompletedTask;
}

/// <summary>
/// Moves records a module keeps about a lead, such as message threads, callbacks or voicemail, to the contact the
/// lead became. Each module that stores such records registers one, so the conversion never needs to know them.
/// </summary>
public interface ILeadConversionRepointer
{
    /// <summary>
    /// Points the module's records about the lead at the contact.
    /// </summary>
    /// <param name="context">The conversion.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RepointAsync(LeadConversionContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// The records a lead conversion reads and produces.
/// </summary>
public sealed class LeadConversionContext
{
    /// <summary>
    /// Gets or sets the request.
    /// </summary>
    public LeadConversionRequest Request { get; set; }

    /// <summary>
    /// Gets or sets the lead.
    /// </summary>
    public ContentItem Lead { get; set; }

    /// <summary>
    /// Gets or sets the contact the lead becomes or is merged into.
    /// </summary>
    public ContentItem Contact { get; set; }

    /// <summary>
    /// Gets or sets whether the contact is created by the conversion.
    /// </summary>
    public bool ContactCreated { get; set; }

    /// <summary>
    /// Gets or sets the account the contact joins.
    /// </summary>
    public ContentItem Account { get; set; }

    /// <summary>
    /// Gets or sets the opportunity the conversion creates.
    /// </summary>
    public ContentItem Opportunity { get; set; }
}
