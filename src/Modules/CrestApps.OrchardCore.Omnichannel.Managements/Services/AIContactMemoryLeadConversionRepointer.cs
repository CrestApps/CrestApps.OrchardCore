using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Moves what the AI remembers about a converted lead to the contact it became, so the AI still knows the customer
/// on the next call or text. When the lead is merged into a contact the AI already remembers, the two notes are kept
/// together, the contact's first.
/// </summary>
public sealed class AIContactMemoryLeadConversionRepointer : ILeadConversionRepointer
{
    private readonly IAIContactMemoryStore _store;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIContactMemoryLeadConversionRepointer"/> class.
    /// </summary>
    /// <param name="store">The contact memory store.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public AIContactMemoryLeadConversionRepointer(
        IAIContactMemoryStore store,
        IClock clock,
        ILogger<AIContactMemoryLeadConversionRepointer> logger)
    {
        _store = store;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RepointAsync(LeadConversionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var leadId = context.Lead?.ContentItemId;
        var contactId = context.Contact?.ContentItemId;

        if (string.IsNullOrEmpty(leadId) || string.IsNullOrEmpty(contactId) || leadId == contactId)
        {
            return;
        }

        var leadMemory = await _store.FindByContactAsync(leadId, cancellationToken);

        if (leadMemory is null)
        {
            return;
        }

        var contactMemory = await _store.FindByContactAsync(contactId, cancellationToken);

        if (contactMemory is null)
        {
            leadMemory.ContactId = contactId;
            await _store.SaveAsync(leadMemory, cancellationToken);
        }
        else
        {
            contactMemory.Text = Merge(contactMemory.Text, leadMemory.Text);
            contactMemory.UpdatedUtc = _clock.UtcNow;
            contactMemory.LastSessionId = null;

            await _store.SaveAsync(contactMemory, cancellationToken);
            await _store.DeleteAsync(leadMemory, cancellationToken);
        }

        _logger.LogInformation("Moved what the AI remembers about a converted lead to the contact it became.");
    }

    /// <summary>
    /// Keeps two notes together, the first one first, within <see cref="AIContactMemory.MaxLength"/>.
    /// </summary>
    /// <param name="first">The note kept first.</param>
    /// <param name="second">The note added after it.</param>
    internal static string Merge(string first, string second)
    {
        first = first?.Trim();
        second = second?.Trim();

        var text = string.IsNullOrEmpty(first)
            ? second
            : string.IsNullOrEmpty(second) || first.Contains(second, StringComparison.OrdinalIgnoreCase)
                ? first
                : first + Environment.NewLine + second;

        return text?.Length > AIContactMemory.MaxLength ? text[..AIContactMemory.MaxLength] : text;
    }
}
