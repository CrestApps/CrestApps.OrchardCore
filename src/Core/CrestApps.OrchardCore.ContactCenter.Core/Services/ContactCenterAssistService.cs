using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterAssistService"/>.
/// </summary>
public sealed class ContactCenterAssistService : IContactCenterAssistService
{
    private readonly IEnumerable<IContactCenterAssistProvider> _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterAssistService"/> class.
    /// </summary>
    /// <param name="providers">The registered assist providers.</param>
    public ContactCenterAssistService(IEnumerable<IContactCenterAssistProvider> providers)
    {
        _providers = providers;
    }

    /// <inheritdoc/>
    public bool IsAvailable => _providers.Any();

    /// <inheritdoc/>
    public async Task<DispositionSuggestion> SuggestDispositionAsync(AssistContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var provider in _providers.OrderBy(provider => provider.Order))
        {
            var suggestion = await provider.SuggestDispositionAsync(context, cancellationToken);

            if (suggestion is not null)
            {
                return suggestion;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<string> SummarizeAsync(AssistContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var provider in _providers.OrderBy(provider => provider.Order))
        {
            var summary = await provider.SummarizeAsync(context, cancellationToken);

            if (!string.IsNullOrEmpty(summary))
            {
                return summary;
            }
        }

        return null;
    }
}
