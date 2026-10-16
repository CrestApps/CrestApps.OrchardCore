using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.AI.Handlers;

/// <summary>
/// Tells the person deleting a persona how many AI profiles used it. A profile linked to a deleted persona keeps
/// working without one, so the delete is allowed; the notice explains why those profiles changed character.
/// </summary>
internal sealed class AIPersonaDeletionHandler : CatalogEntryHandlerBase<AIPersona>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIPersonaDeletionHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider, used to reach the managers only when a persona is deleted.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public AIPersonaDeletionHandler(
        IServiceProvider serviceProvider,
        INotifier notifier,
        IHtmlLocalizer<AIPersonaDeletionHandler> htmlLocalizer)
    {
        _serviceProvider = serviceProvider;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <inheritdoc />
    public override async Task DeletedAsync(DeletedContext<AIPersona> context, CancellationToken cancellationToken = default)
    {
        var persona = context.Model;

        if (persona is null || string.IsNullOrWhiteSpace(persona.Name))
        {
            return;
        }

        // Resolved here rather than injected: the persona manager builds its handlers, so it cannot be one of their
        // dependencies, and the profile manager has no reason to be constructed for every persona save.
        var personaManager = _serviceProvider.GetRequiredService<IAIPersonaManager>();

        // A persona file with the same name takes the links over, so the profiles keep a persona.
        if (await personaManager.FindByNameAsync(persona.Name, cancellationToken) is not null)
        {
            return;
        }

        var profileManager = _serviceProvider.GetRequiredService<IAIProfileManager>();
        var profiles = await profileManager.GetAllAsync(cancellationToken);

        var linked = profiles.Count(profile =>
            profile.TryGet<AIPersonaMetadata>(out var metadata) &&
            string.Equals(metadata.PersonaId, persona.Name, StringComparison.OrdinalIgnoreCase));

        if (linked == 0)
        {
            return;
        }

        await _notifier.WarningAsync(H.Plural(
            linked,
            "One AI profile used the persona '{1}' and now runs without a persona.",
            "{0} AI profiles used the persona '{1}' and now run without a persona.",
            persona.DisplayText ?? persona.Name));
    }
}
