using CrestApps.AI.Models;
using Microsoft.Extensions.Options;

namespace CrestApps.AI.Services;

/// <summary>
/// Default options-backed implementation of <see cref="IInteractionDocumentSettingsProvider"/>.
/// Hosts can replace this with a tenant-aware implementation.
/// </summary>
public sealed class DefaultInteractionDocumentSettingsProvider : IInteractionDocumentSettingsProvider
{
    private readonly IOptionsSnapshot<InteractionDocumentSettings> _settings;

    public DefaultInteractionDocumentSettingsProvider(IOptionsSnapshot<InteractionDocumentSettings> settings)
    {
        _settings = settings;
    }

    public Task<InteractionDocumentSettings> GetAsync() => Task.FromResult(_settings.Value);
}
