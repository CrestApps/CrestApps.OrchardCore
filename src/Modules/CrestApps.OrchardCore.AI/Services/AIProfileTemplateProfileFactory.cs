using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Builds a profile from a template on the server and persists it, without a round trip through the profile
/// editor.
/// </summary>
/// <remarks>
/// The editor rebuilds a new profile from the fields it renders, so a template value with no field of its own
/// never reaches a profile saved there. Building the profile here keeps every value the template carries.
/// </remarks>
internal sealed class AIProfileTemplateProfileFactory
{
    private readonly IAIProfileManager _profileManager;
    private readonly IEnumerable<IAIProfileTemplateApplicationHandler> _handlers;
    private readonly DefaultAIOptions _defaultAIOptions;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplateProfileFactory"/> class.
    /// </summary>
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="handlers">The handlers that finish a profile built from a template before it is persisted.</param>
    /// <param name="defaultAIOptions">The default AI options that fill parameters the template leaves empty.</param>
    /// <param name="logger">The logger.</param>
    public AIProfileTemplateProfileFactory(
        IAIProfileManager profileManager,
        IEnumerable<IAIProfileTemplateApplicationHandler> handlers,
        IOptions<DefaultAIOptions> defaultAIOptions,
        ILogger<AIProfileTemplateProfileFactory> logger)
    {
        _profileManager = profileManager;
        _handlers = handlers;
        _defaultAIOptions = defaultAIOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new, unsaved profile with its identifier assigned and the template's values applied.
    /// </summary>
    /// <param name="template">The template to build the profile from.</param>
    /// <returns>The new profile, or <see langword="null"/> when the profile manager could not create one.</returns>
    /// <remarks>
    /// Nothing is persisted and no <see cref="IAIProfileTemplateApplicationHandler"/> runs yet, so a profile
    /// that fails validation afterwards leaves nothing behind. Call <see cref="CreateAsync"/> to persist it.
    /// </remarks>
    public async Task<AIProfile> NewFromTemplateAsync(AIProfileTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        var profile = await _profileManager.NewAsync();

        if (profile is null)
        {
            return null;
        }

        AIProfileTemplateApplicator.Apply(profile, template);

        PopulateDefaultParameters(profile);

        return profile;
    }

    /// <summary>
    /// Lets every <see cref="IAIProfileTemplateApplicationHandler"/> finish the profile, then persists it.
    /// </summary>
    /// <param name="profile">A profile returned by <see cref="NewFromTemplateAsync"/> that passed validation.</param>
    /// <param name="template">The template the profile was built from.</param>
    public async Task CreateAsync(AIProfile profile, AIProfileTemplate template)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(template);

        var context = new AIProfileTemplateAppliedContext(profile, template);

        await _handlers.InvokeAsync((handler, ctx) => handler.AppliedAsync(ctx), context, _logger);

        await _profileManager.CreateAsync(profile);
    }

    /// <summary>
    /// Fills the model parameters the template leaves empty with the configured defaults.
    /// </summary>
    /// <remarks>
    /// The editor shows these defaults in its fields and saves whatever the fields hold, so this produces the
    /// profile that saving the prefilled editor would have. The past messages count is kept only for a chat
    /// profile, because that is the only type the editor saves it for, and a chat profile cannot be saved
    /// without one.
    /// </remarks>
    private void PopulateDefaultParameters(AIProfile profile)
    {
        var metadata = profile.GetOrCreate<AIProfileMetadata>();

        metadata.FrequencyPenalty ??= _defaultAIOptions.FrequencyPenalty;
        metadata.PresencePenalty ??= _defaultAIOptions.PresencePenalty;
        metadata.Temperature ??= _defaultAIOptions.Temperature;
        metadata.MaxTokens ??= _defaultAIOptions.MaxOutputTokens;
        metadata.TopP ??= _defaultAIOptions.TopP;

        if (profile.Type == AIProfileType.Chat)
        {
            metadata.PastMessagesCount ??= _defaultAIOptions.PastMessagesCount;
        }

        profile.Put(metadata);
    }
}
